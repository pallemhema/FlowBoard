# FlowBoard - Mandatory Project Instructions

These instructions are the permanent coding and architecture standards for the FlowBoard repository.

## Mandatory Instruction Priority

1. These instructions apply to every task, implementation phase, code change, refactoring, bug fix, and feature developed in this repository.
2. Every subsequent prompt or task must follow these instructions.
3. Treat these standards as persistent project-level requirements, not optional recommendations.
4. A later task-specific prompt may define the feature scope, requirements, and implementation details, but must not silently override these standards.
5. If a requested implementation conflicts with these standards or the existing architecture, identify the conflict and explain it before making changes.
6. Inspect the existing implementation before modifying or creating files.
7. Do not unnecessarily change previously agreed architectural decisions.
8. Do not introduce new abstractions, projects, dependencies, entities, tables, or patterns without a clear requirement.
9. Preserve existing functionality unless the task explicitly requires changing it.
10. Before completing a task, verify that the implementation follows these instructions and builds successfully.

## Authentication and Authorization

- Use ASP.NET Core Policy-Based Authorization.
- Define centralized authorization policies in Program.cs or an appropriately organized authorization configuration.
- Use strict role-based policies for Admin, Manager, and Employee.
- Use `[Authorize(Policy = "...")]` on controllers and endpoints.
- Avoid scattered manual role checks and repeated ClaimsPrincipal inspection.
- Use resource-level authorization checks for workspace, board, column, card, and membership access.
- Never rely exclusively on frontend button visibility for authorization.
- Never allow public registration to assign privileged roles.
- Follow least-privilege access principles.

## Data Validation

- Use Data Annotations such as Required, StringLength, Range, EmailAddress, and custom ValidationAttribute where appropriate.
- Apply validation consistently to request DTOs.
- Use EF Core Fluent API for relationships, indexes, unique constraints, delete behavior, and database-specific configuration.
- Keep business validation inside the appropriate service layer.
- Do not duplicate validation unnecessarily.

## Clean Code

- Keep controllers thin.
- Keep business logic inside services.
- Use interfaces and dependency injection consistently.
- Keep DTOs in FlowBoard.Shared.
- Keep entities in FlowBoard.Domain.
- Keep database configurations in FlowBoard.Infrastructure.
- Use asynchronous database operations.
- Use transactions for operations requiring multiple related database changes.
- Use clear naming, focused methods, and reusable code.
- Avoid unnecessary abstractions, duplicated code, and overengineering.
- Follow existing project conventions.

## Scope and Implementation

- Preserve existing functionality.
- Inspect existing implementations before creating new entities, services, controllers, or tables.
- Reuse existing functionality wherever appropriate.
- Create forward-only migrations.
- Never modify previously applied migrations.
- Ensure seed data is safe and restricted to development environments.
- Add meaningful unit and integration tests, especially for authorization and access restrictions.

## Completion Requirements

Before considering any task complete:

- Verify that all affected projects compile.
- Verify that existing functionality has not been unintentionally broken.
- Verify authorization and access restrictions.
- Verify DTO and database validation.
- Verify that no previously applied migration was modified.
- Add or update relevant tests.
- Report the files changed, architectural decisions, migrations created, tests executed, and any remaining issues.

These standards are mandatory for all FlowBoard development.