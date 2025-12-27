# Ominigo IoT Dispatcher Worker

Background worker that pulls dispatch queue items from the Ominigo API, stages them in MongoDB, and dispatches bookings to the nearest available vehicle. Built on the .NET worker template with HttpClientFactory, MongoDB.Driver, and Serilog rolling file logs.

## Configuration
- Update `DispatcherService/appsettings.json` with your values:
  - `Worker`: toggle `Enabled`, set `InstanceId`, polling intervals, `BatchSize`, and `MaxRetry`.
  - `Api`: `BaseUrl`, `GetQueueEndpoint`, `UpdateQueueEndpoint`, and `AuthToken` (Bearer token).
  - `Mongo`: `ConnectionString`, `Database`, `QueueCollection`, `VehicleLiveCollection`.
- Logs write to `Logs/` (rolling daily files).
- `Mongo.VehicleLiveCollection` defaults to `location_last_update` (fields `lat`/`lon`; if `loc` GeoJSON exists it will be used for $geoNear, otherwise the worker falls back to in-memory distance calc).

## Run locally
1) `cd DispatcherService`
2) `dotnet restore`
3) `dotnet run`

## Publish + install as Windows service
1) Publish: `dotnet publish -r win-x64 -c Release --self-contained false`
2) Install: `sc create OminigoIoTDispatcher binPath= "<publish_path>\\DispatcherService.exe" start= auto`
3) Start: `sc start OminigoIoTDispatcher`
4) Stop: `sc stop OminigoIoTDispatcher`
5) Remove: `sc delete OminigoIoTDispatcher`

## Behavior
- Queue puller (every `PollIntervalSeconds`): POSTs `GetQueueEndpoint`, upserts each item into Mongo with `state=pending_dispatch`, and keeps retry counts in sync.
- Dispatcher (every `DispatchPollIntervalSeconds`): atomically claims one pending job, runs `$geoNear` on `vehicle_live.loc` to find the closest free vehicle within `radius_km`, calls `UpdateQueueEndpoint`, and marks Mongo docs as done/failed/pending with retry limits respected.
- Mongo indexes created on startup:
  - `dispatch_queue`: unique on `dispatch_queue_uno`, and on `state`.
  - `vehicle_live`: 2dsphere on `loc`.
