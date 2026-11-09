# AI Usage Disclosure

## 1. Project Information

**Project Name:** School Admission System

**Technology Stack:**
- Backend: C# / .NET 8
- Frontend: React 18 / TypeScript
- Database: PostgreSQL
- Containerization: Docker and Docker Compose

## 2. AI Tools Used

ChatGPT was used as an assistance tool during the development of this project.

## 3. How AI Was Used

AI assistance was used for the following activities:

- Planning the overall project structure and application workflow.
- Generating and refining initial code examples for the .NET Web API, PostgreSQL integration, React frontend, and Docker configuration.
- Understanding backend architecture, API endpoints, database relationships, and frontend-backend communication.
- Implementing and understanding temporary seat reservations, automatic hold expiration, admission confirmation, and seat availability management.
- Troubleshooting build errors and configuration issues.
- Reviewing project documentation and suggesting testing scenarios.

AI was used as a development assistant rather than a replacement for understanding the code and development process.

## 4. Identifying and Correcting AI-Generated Mistakes

During development, I encountered a frontend compilation issue where TypeScript could not recognize `import.meta.env`.

I investigated the build error and identified that the Vite environment type declaration was missing.

I resolved the issue independently by creating a `vite-env.d.ts` file inside `frontend/src` and adding the following declaration:

```typescript
/// <reference types="vite/client" />
```

After making this correction, I rebuilt the application using Docker Compose and verified that the frontend started successfully.

This experience helped me understand the importance of reading error messages, identifying root causes, and validating AI-generated code rather than blindly accepting it.

## 5. Code Review and Logical Validation

During development, I also reviewed the admission workflow to understand and validate important business rules.

Some of the areas I focused on included:

- Ensuring that expired reservations release their seats automatically.
- Preventing students from having multiple active applications.
- Checking that students cannot reserve seats when a class has reached its capacity.
- Considering simultaneous application requests from multiple students.
- Reviewing repeated confirmation requests to understand idempotency.
- Checking whether application data remains available after restarting the Docker containers.

These scenarios helped me understand the importance of handling edge cases, maintaining data consistency, and preventing race conditions in systems where multiple users compete for limited resources.

## 6. Human Involvement and Responsibility

I configured the development environment, installed and configured Docker Desktop, ran the application locally, and performed manual testing through the frontend and API.

I also investigated configuration errors, applied corrections, and verified the application's behavior through practical testing.

I remain responsible for reviewing, understanding, validating, and maintaining the submitted source code. AI-generated suggestions were not considered automatically correct.

## 7. Conclusion

AI assistance helped accelerate the development process and improve my understanding of unfamiliar technologies. However, identifying issues, understanding the business requirements, validating application behavior, and taking responsibility for the final implementation were important parts of my development process.

The project is submitted with the understanding that AI assistance does not replace independent verification or technical knowledge.