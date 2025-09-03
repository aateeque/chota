# Chota

The URL shortening service written in C# .NET 9

## Design

![arch](arch.png)

Chota is a URL shortening service. The various components that make it up are:

1. API:
   - Exposes RESTful endpoints for URL shortening and redirection
   - The API relies on PosgreSQL for persistence
   - It uses Redis for caching
   - Given a `LongUrl`, it generates a unique `ShortCode` that maps to the original URL

2. PostgreSQL - Chota:
   - Provides persistent storage for URL mappings to shortCodes
   - Supports efficient querying and retrieval

3. Redis:
   - Redis provides an efficient caching layer
   - It improves performance and reduces database load

4. Migrations:
   - Handles database schema changes and versioning
   - Ensures smooth upgrades and rollbacks
   - It uses [EntityFramework Core Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations) and is a standalone console application

5. Chota-redishydrator (tbd):
    - A background service that synchronizes data between PostgreSQL and Redis
    - The idea with this was to ensure at startup to preload the cache with existing URL mappings

The whole solution is built on top of [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/get-started/aspire-overview) so it allows for easy local orchestration and development. I also ship PgAdmin and RedisCommander for easy UI access to the data layer. 

See the URLs in the Resources tab on the [Aspire Dashboard](https://localhost:17024/).

## Prerequisites

You will need the following to run Chota:

- [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet)
- [Docker](https://www.docker.com/)
- Aspire CLI: `curl -sSL https://aspire.dev/install.sh | bash` (used for local development orchestration)

## Getting Started

1. Clone the repository
2. Run `dotnet tool restore` to restore the tools
3. Run `dotnet workload restore` to install the required workloads
4. Run `aspire run` to start the services

## Usage

1. Ensure Aspire Dashboard is up & running and all the services are ready with `aspire run` (you'll need [Aspire cli ](https://learn.microsoft.com/en-us/dotnet/aspire/cli/overview) from above):\
  ![running stack](runningStack.png)
2. Access the API at `https://localhost:7500/`
3. Explore the API using the [Scalar UI](https://scalar.com/)
4. Interact with the API endpoints as needed
5. We ship _[Redis Commander](http://localhost:53662/)_ as well as _[pgAdmin](http://localhost:53664/)_ for an in-browser database management experience; browse to the UI from the Aspire Dashboard
