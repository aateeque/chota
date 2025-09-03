# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Chota is a URL shortening service built with .NET 9 and .NET Aspire. It uses a microservices architecture with PostgreSQL for persistence, Redis for caching, and Entity Framework Core for data access.

## Architecture

The solution uses .NET Aspire for orchestration and consists of these projects:

- **Chota.AppHost** - .NET Aspire orchestrator that coordinates all services and dependencies
- **Chota.Api** - Main API service providing URL shortening endpoints
- **Chota.Web** - Web frontend application  
- **Chota.MigrationService** - Database migration service that runs EF Core migrations
- **Chota.RedisHydrator** - Background service for Redis data hydration
- **Chota.ServiceDefaults** - Shared Aspire service configuration and telemetry
- **Chota.Tests** - Test project using TUnit, NBomber for load testing, and Testcontainers

Key infrastructure components:
- PostgreSQL database with unique constraint on `LongUrl`
- Redis cache for performance optimization
- Composite repository pattern (memory → Redis → PostgreSQL)
- ID generation using IdGen library with Base62 encoding
- Click tracking for browser vs API requests

## Development Commands

### Building and Running
```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run with Aspire orchestrator (recommended for development)
dotnet run --project src/Chota.AppHost

# Build without restoring
dotnet build --no-restore
```

### Testing
```bash
# Run all tests
dotnet test

# Run tests with specific verbosity
dotnet test --verbosity normal

# Run tests without building first
dotnet test --no-build
```

### Database Operations
```bash
# Install EF Core tools (if not already installed)
dotnet tool restore

# Add new migration
dotnet ef migrations add <MigrationName> --project src/Chota.Api

# Update database (usually handled by MigrationService)
dotnet ef database update --project src/Chota.Api
```

## Key Technical Details

- **Target Framework**: .NET 9.0
- **Database**: PostgreSQL with EF Core migrations
- **Caching**: Redis with StackExchange.Redis
- **ID Generation**: IdGen library for distributed ID generation
- **URL Encoding**: Base62 encoding for short codes
- **API Documentation**: Scalar UI available at `/scalar` endpoint when running
- **Health Checks**: Available at `/health` endpoint
- **Testing**: TUnit framework with Testcontainers for integration tests

## Service Dependencies

The AppHost orchestrates service startup order:

1. PostgreSQL and Redis containers
2. Database migration service
3. API service (depends on successful migrations)
4. Background services (RedisHydrator)

## Development Workflow

1. Use `aspire run` to start all services
2. Access the API documentation via Scalar UI
3. The AppHost provides URLs for all services including Redis Commander and pgAdmin
4. Tests use Testcontainers for isolated integration testing