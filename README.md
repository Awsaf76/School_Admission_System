# School Admission System

## Stack
ASP.NET Core Web API (.NET 8), PostgreSQL 16, Entity Framework Core, React 18 + TypeScript + Vite, Axios, CSS, Docker Compose.

## Run the complete project
1. Install and open Docker Desktop.
2. Extract this repository.
3. Open a terminal in the folder containing `docker-compose.yml`.
4. Run `docker compose up --build`.
5. Open the frontend at http://localhost:5173.
6. Backend API: http://localhost:8080. Swagger: http://localhost:8080/swagger.

To stop: press Ctrl+C, then run `docker compose down`. Do not use `-v` if you want to preserve database data. PostgreSQL data is stored in a named Docker volume.

## Configure hold time
Default is 600 seconds. Windows PowerShell:
`$env:HOLD_DURATION_SECONDS=60`
`docker compose up --build`

Linux/macOS:
`HOLD_DURATION_SECONDS=60 docker compose up --build`

## Run locally without Docker
Requires .NET 8 SDK, Node.js 20+, and PostgreSQL.
- Backend: `cd backend`, configure `ConnectionStrings:DefaultConnection`, then `dotnet restore` and `dotnet run`.
- Frontend: `cd frontend`, run `npm install` and `npm run dev`.

## API
- POST `/classes`
- GET `/classes`
- GET `/classes/{classId}`
- POST `/classes/{classId}/holds` (header `X-Student-Id`)
- GET `/holds/{holdId}`
- POST `/holds/{holdId}/confirm` (header `X-Student-Id`)
- DELETE `/holds/{holdId}` (header `X-Student-Id`)
- GET `/me/holds` (header `X-Student-Id`)

## Design highlights
- PostgreSQL row locking serializes seat allocation per class.
- PostgreSQL transaction advisory locks serialize simultaneous applications from the same student.
- A partial unique index prevents duplicate HELD/CONFIRMED records for a student.
- A hosted worker expires overdue holds every two seconds; API reads also refresh expired records.
- Confirming an already-confirmed hold is idempotent.
- Payment is simulated, not connected to a real payment gateway.
