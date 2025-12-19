# Voting System

A web-based voting system built with ASP.NET Core and PostgreSQL.

## Features

- Create and manage voting sessions
- Add candidates to sessions
- Vote in published sessions (single or multi-choice)
- View voting results
- Admin panel for session management

## Prerequisites

- .NET 10.0 SDK
- PostgreSQL database
- Npgsql driver (included)

## Database Setup

1. Create a PostgreSQL database:
   ```sql
   CREATE DATABASE voting;
   ```

2. Run the SQL scripts from `init_database.sql` or execute the schema creation SQL provided in the lab requirements.

3. The application uses the following connection string (update in `appsettings.json` if needed):
   ```
   Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=voting
   ```

## Running the Application

1. Ensure PostgreSQL is running and the database is set up.

2. Restore dependencies:
   ```bash
   dotnet restore
   ```

3. Run the application:
   ```bash
   dotnet run
   ```

4. Open your browser and navigate to the displayed URL (usually `http://localhost:5158`)

## Usage

- **Home Page**: View published voting sessions
- **Vote**: Select candidates and submit your vote
- **Results**: View voting results for each session
- **Admin Panel**: Create sessions, add candidates, publish sessions

## Database Interaction

The application uses raw SQL queries and stored procedures for all database operations, as required by the lab specifications.

## Architecture

- **Models**: Data models for database entities
- **Services**: Business logic and database access layer
- **Pages**: Razor Pages for UI