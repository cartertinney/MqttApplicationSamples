# :point_right: Telemetry (Fan-in)

| [Create Client Certificates](#lock-create-client-certificates) | [Configure Event Grid Namespaces](#triangular_ruler-configure-event-grid-namespaces) | [Configure Mosquitto](#fly-configure-mosquitto) | [Run the Sample](#game_die-run-the-sample) |

This scenario shows how multiple clients send data (the producers) to different topics that can be consumed by a single application (the consumer).  This scenario also showcases routing the data to an Azure service.

Consider a use case where a backend solution needs to identify the location of vehicles on a map. Vehicles should be prohibited from listening to other vehicles location on their behalf. Finally, the location data need to be routed to a storage queue.

|Client|Role|Operation|Topic/Topic Filter|
|------|----|---------|------------------|
|vehicle1|producer|pub|vehicles/vehicle1/position|
|vehicle2|producer|pub|vehicles/vehicle2/position|
|map-app|consumer|sub|vehicles/+/position|

Messages will use [GeoJSON](https://geojson.org) to represent the coordinates.

```json
{
    "type": "Point",
    "coordinates": [125.6, 10.1]
}
```

## :lock: Create Client Certificates

Run the following step commands to create the client certificates for `vehicle01`, `vehicle02` and `map-app` clients.

```bash
cd scenarios/telemetry
step certificate create \
    vehicle01 vehicle01.pem vehicle01.key \
    --ca ~/.step/certs/intermediate_ca.crt \
    --ca-key ~/.step/secrets/intermediate_ca_key \
    --no-password --insecure \
    --not-after 2400h

step certificate create \
    vehicle02 vehicle02.pem vehicle02.key \
    --ca ~/.step/certs/intermediate_ca.crt \
    --ca-key ~/.step/secrets/intermediate_ca_key \
    --no-password --insecure \
    --not-after 2400h

step certificate create \
    map-app map-app.pem map-app.key \
    --ca ~/.step/certs/intermediate_ca.crt \
    --ca-key ~/.step/secrets/intermediate_ca_key \
    --no-password --insecure \
    --not-after 2400h

```

## :triangular_ruler: Configure Event Grid Namespaces

Event Grid Namespaces requires to register the clients, and the topic spaces to set the client permissions.

### Create the clients

The clients will be created based on the certificate subject, you can register the 3 clients in the portal or by running the script below:

```bash
source ../../az.env

az resource create --id "$res_id/clients/vehicle01" --properties '{
    "authenticationName": "vehicle01",
    "state": "Enabled",
    "clientCertificateAuthentication": {
        "validationScheme": "SubjectMatchesAuthenticationName"
    },
    "attributes": {
            "type": "vehicle"
    },
    "description": "This is a test publisher client"
}'

az resource create --id "$res_id/clients/vehicle02" --properties '{
    "authenticationName": "vehicle02",
    "state": "Enabled",
    "clientCertificateAuthentication": {
        "validationScheme": "SubjectMatchesAuthenticationName"
    },
    "attributes": {
            "type": "vehicle"
    },
    "description": "This is a test publisher client"
}'

az resource create --id "$res_id/clients/map-app" --properties '{
    "authenticationName": "map-app",
    "state": "Enabled",
    "clientCertificateAuthentication": {
        "validationScheme": "SubjectMatchesAuthenticationName"
    },
    "attributes": {
            "type": "map-client"
    },
    "description": "This is a test subscriber client"
}'

```

### Configure topic spaces and permission bindings

```bash
az resource create --id "$res_id/topicSpaces/vehicles" --properties '{
    "topicTemplates": ["vehicles/#"]
}'

az resource create --id "$res_id/permissionBindings/vehiclesPub" --properties '{
    "clientGroupName":"$all",
    "topicSpaceName":"vehicles",
    "permission":"Publisher"
}'

az resource create --id "$res_id/permissionBindings/vehiclesSub" --properties '{
    "clientGroupName":"$all",
    "topicSpaceName":"vehicles",
    "permission":"Subscriber"
}'
```

### Create the .env files with connection details

The required `.env` files can be configured manually, we provide the script below as a reference to create those files, as they are ignored from git.

```bash
source ../../az.env
host_name=$(az resource show --ids $res_id --query "properties.topicSpacesConfiguration.hostname" -o tsv)

echo "MQTT_HOST_NAME=$host_name" > vehicle01.env
echo "MQTT_USERNAME=vehicle01" >> vehicle01.env
echo "MQTT_CLIENT_ID=vehicle01" >> vehicle01.env
echo "MQTT_CERT_FILE=vehicle01.pem" >> vehicle01.env
echo "MQTT_KEY_FILE=vehicle01.key" >> vehicle01.env
echo "MQTT_CA_PATH=/etc/ssl/certs" >> vehicle01.env # required by mosquitto_lib to validate EG Tls cert 


echo "MQTT_HOST_NAME=$host_name" > vehicle02.env
echo "MQTT_USERNAME=vehicle02" >> vehicle02.env
echo "MQTT_CLIENT_ID=vehicle02" >> vehicle02.env
echo "MQTT_CERT_FILE=vehicle02.pem" >> vehicle02.env
echo "MQTT_KEY_FILE=vehicle02.key" >> vehicle02.env
echo "MQTT_CA_PATH=/etc/ssl/certs" >> vehicle02.env # required by mosquitto_lib to validate EG Tls cert 

echo "MQTT_HOST_NAME=$host_name" > map-app.env
echo "MQTT_USERNAME=map-app" >> map-app.env
echo "MQTT_CLIENT_ID=map-app" >> map-app.env
echo "MQTT_CERT_FILE=map-app.pem" >> map-app.env
echo "MQTT_KEY_FILE=map-app.key" >> map-app.env
echo "MQTT_CA_PATH=/etc/ssl/certs" >> map-app.env # required by mosquitto_lib to validate EG Tls cert 
```

## :fly: Configure Mosquitto

To establish the TLS connection, the CA needs to be trusted, most MQTT clients allow to specify the ca trust chain as part of the connection, to create a chain file with the root and the intermediate use:

```bash
cat ~/.step/certs/root_ca.crt ~/.step/certs/intermediate_ca.crt > chain.pem
```
The `chain.pem` is used by mosquitto via the `cafile` settings to authenticate X509 client connections.

```bash
echo "MQTT_HOST_NAME=localhost" > vehicle01.env
echo "MQTT_CERT_FILE=vehicle01.pem" >> vehicle01.env
echo "MQTT_KEY_FILE=vehicle01.key" >> vehicle01.env
echo "MQTT_CA_FILE=chain.pem" >> vehicle01.env

echo "MQTT_HOST_NAME=localhost" > vehicle02.env
echo "MQTT_CERT_FILE=vehicle02.pem" >> vehicle02.env
echo "MQTT_KEY_FILE=vehicle02.key" >> vehicle02.env
echo "MQTT_CA_FILE=chain.pem" >> vehicle02.env

echo "MQTT_HOST_NAME=localhost" > map-app.env
echo "MQTT_CERT_FILE=map-app.pem" >> map-app.env
echo "MQTT_KEY_FILE=map-app.key" >> map-app.env
echo "MQTT_CA_FILE=chain.pem" >> map-app.env

```

To use mosquitto without certificates: change the port to 1883, disable TLS and set the CA_FILE

```bash
echo "MQTT_HOST_NAME=localhost" > vehicle01.env
echo "MQTT_TCP_PORT=1883" >> vehicle01.env
echo "MQTT_USE_TLS=false" >> vehicle01.env
echo "MQTT_CLIENT_ID=vehicle01" >> vehicle01.env
```

## :game_die: Run the Sample

All samples are designed to be executed from the root scenario folder.

### dotnet

To build the dotnet sample run:

```bash
dotnet build dotnet/telemetry.sln 
```

To run the dotnet sample execute each line below in a different shell/terminal.

```bash
 dotnet/telemetry_producer/bin/Debug/net7.0/telemetry_producer --envFile=vehicle01.env
 dotnet/telemetry_producer/bin/Debug/net7.0/telemetry_producer --envFile=vehicle02.env
 dotnet/telemetry_consumer/bin/Debug/net7.0/telemetry_consumer --envFile=map-app.env
```

### C

To build the C sample, run from the root folder:

```bash
cmake --preset=telemetry
cmake --build --preset=telemetry
```

The build script will copy the produced binary to `c/build/telemetry`

To run the C sample execute each line below in a different shell/terminal (from the root scenario folder `scenarios/telemetry`).

```bash
c/build/telemetry_producer vehicle01.env
c/build/telemetry_producer vehicle02.env
c/build/telemetry_consumer map-app.env
```