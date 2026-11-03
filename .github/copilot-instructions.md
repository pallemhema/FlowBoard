# FlowBoard - UI Design and Frontend Development Instructions

These instructions define the permanent UI design direction, frontend architecture principles, and user experience standards for the FlowBoard application.

They must be followed for all Phase 5 UI planning, design, and implementation tasks.

## 1. Overall UI Design Vision

FlowBoard should be a professional, modern, enterprise-level project and task management application.

The UI should take inspiration from:

- Linear: Clean, minimal, modern interface and excellent user experience.
- Jira: Project organization, boards, task management, and workflow functionality.
- Modern SaaS applications: Consistent layouts, reusable components, intuitive navigation, and responsive design.

Do not directly copy another application's interface. Develop a consistent and recognizable visual identity for FlowBoard.

The primary goal is to create a UI that looks professional, feels intuitive, and is easy to use without unnecessary complexity.

## 2. Visual Design System

Follow a consistent design system throughout the application.

### Color Palette
- Primary color: Indigo or professional blue.
- Main background: Light gray or subtle neutral background.
- Content surfaces: White.
- Sidebar: Dark navy or a clean white sidebar with appropriate contrast.
- Primary text: Dark charcoal.
- Secondary text: Neutral gray.
- Success: Green.
- Warning: Orange or amber.
- Error: Red.
- Borders: Subtle light gray.

Avoid excessive gradients, bright colors, heavy shadows, and unnecessary visual effects.

### Typography
- Use a modern, readable sans-serif font.
- Establish consistent heading levels and text sizes.
- Maintain clear visual hierarchy between page titles, section headings, labels, and supporting text.

### UI Appearance
- Consistent spacing and alignment.
- Subtle borders and shadows.
- Appropriate border radius.
- Clean cards and tables.
- Consistent icons.
- Professional status and priority indicators.
- Smooth but minimal transitions.
- Accessible color contrast.

The application should look polished and consistent across every page.

## 3. Application Layout

Use one shared application layout for Admin, Manager, and Employee.

The common layout should contain:

### Sidebar
- Primary application navigation.
- Role-specific navigation items.
- Expandable or collapsible behavior.
- Clear active-page indication.
- Consistent icons and labels.

### Top Navigation Bar
- Application branding.
- Global search, where applicable.
- Notifications.
- User profile and account menu.
- Logout option.

### Main Content Area
- Page title and description.
- Breadcrumb navigation where useful.
- Primary page actions.
- Consistent content spacing.
- Reusable page components.

Do not create three separate applications for the three roles. Use a shared Blazor application with role-aware navigation and dashboards.

## 4. Role-Specific User Experiences

FlowBoard supports three primary roles:

- Admin
- Manager
- Employee

Each role must have an appropriate dashboard, navigation structure, and set of available actions.

The UI must reflect the existing backend authorization rules.

### Admin Experience

Admin has an organization-wide management experience.

Suggested navigation:

- Dashboard
- Users
- Teams
- Manager Assignments
- Workspaces
- Projects
- Boards
- My Tasks
- Profile
- Settings

Admin dashboard should provide an organization-wide overview.

Potential dashboard information:
- Total users.
- Managers and employees.
- Active workspaces.
- Active projects.
- Project status overview.
- Recent activity.
- User distribution.
- Quick actions.

Admin should be able to access administrative management features according to the existing backend permissions.

### Manager Experience

Manager has a team and project management experience.

Suggested navigation:

- Dashboard
- My Tasks
- My Boards
- My Employees
- Workspaces
- Projects
- Profile
- Settings

Manager dashboard should focus on:
- Managed employees.
- Owned or accessible workspaces.
- Managed projects.
- Project progress.
- Employee workload.
- Recent activity.
- Tasks assigned to the Manager.

Managers must only see employees linked through the existing ManagerEmployees relationship.

Managers must not see organization-wide administrative functionality unless explicitly authorized.

### Employee Experience

Employee has a task-focused experience.

Suggested navigation:

- Dashboard
- My Tasks
- My Boards
- My Projects
- My Workspaces
- Profile
- Settings

Employee dashboard should focus on:
- Total assigned tasks.
- In-progress tasks.
- Completed tasks.
- Upcoming deadlines.
- Recently accessed boards.
- Accessible projects.
- Recent activity.

The Employee experience should be simple and focused on completing assigned work.

Employees must not see administrative or management functionality.

## 5. Workspace Experience

Workspaces are an important part of FlowBoard.

Every workspace should have a dedicated overview page.

The workspace page should clearly present:

- Workspace name.
- Description.
- Workspace owner.
- Workspace members.
- Projects.
- General/Learning boards.
- Recent activity.

Use an organized tab-based layout where appropriate.

Suggested tabs:

- Overview
- Projects
- Boards
- Members
- Activity

Available actions must depend on the user's existing backend permissions.

Important domain rule:

Workspace membership, project membership, board membership, team membership, manager-employee relationships, and task assignments are separate relationships.

Never assume that membership in one automatically grants membership or access in another.

The UI must respect these distinctions.

## 6. Project Experience

Every project should have a dedicated project details page.

Suggested project tabs:

- Overview
- Members
- Boards
- Activity

Display:
- Project name and description.
- Project status.
- Workspace.
- Project manager.
- Project members.
- Associated boards.
- Relevant progress information.

Available actions should reflect the existing authorization rules.

Do not introduce new backend permissions or relationships just to simplify the UI.

## 7. Board and Task Management Experience

The Kanban board is one of the most important areas of FlowBoard.

The board interface should be clean, responsive, and easy to operate.

### Board Header
Display:
- Board name.
- Project or workspace context.
- Board members.
- Search.
- Filters.
- Add Card action, when permitted.

### Kanban Columns

Display board columns horizontally.

Cards should be organized under their respective columns.

Each card should display relevant information:

- Card title.
- Priority.
- Assignee.
- Due date.
- Comment count.
- Labels, if supported by the existing model.

### Card Details

Clicking a card should open a polished detail panel or modal.

Display:
- Title.
- Description.
- Status.
- Priority.
- Assignee.
- Due date.
- Comments.
- Relevant card information.

Card editing, deletion, assignment, and comment actions must respect the existing backend authorization rules.

Do not implement frontend-only permission enforcement.

The backend remains the final authority.

## 8. Reusable UI Components

Build reusable components wherever appropriate.

Potential shared components include:

- AppSidebar
- AppTopbar
- PageHeader
- Breadcrumb
- StatCard
- DataTable
- UserAvatar
- StatusBadge
- PriorityBadge
- SearchInput
- FilterDropdown
- ConfirmDialog
- ToastNotification
- EmptyState
- LoadingState
- KanbanBoard
- KanbanColumn
- TaskCard
- TaskDetailsPanel

These are suggested components, not mandatory new components.

Before creating a component, inspect the existing implementation and reuse existing components wherever possible.

Avoid unnecessary component fragmentation and overengineering.

## 9. Blazor Development Standards

- Use the existing Blazor architecture and project structure.
- Keep reusable UI components separate from page-specific components.
- Follow existing dependency injection and service conventions.
- Use strongly typed models and DTOs.
- Avoid placing business logic directly inside Razor components.
- Keep components focused and maintainable.
- Use asynchronous operations for API communication.
- Handle loading, error, empty, and success states.
- Avoid unnecessary JavaScript interop.
- Use JavaScript only when Blazor or existing libraries cannot reasonably provide the required functionality.
- Preserve existing authentication and API integration.
- Do not duplicate backend business logic in the frontend.

## 10. Authorization and UI Visibility

The frontend must reflect the existing backend authorization model.

- Show navigation items according to the authenticated user's permissions.
- Hide actions that the current user is not permitted to perform.
- Respect workspace, project, and board access.
- Respect board roles such as Owner, Editor, and Viewer.
- Respect card and comment permissions.
- Do not assume that a user can access a resource merely because it is visible in the UI.

Hiding a button is only a user experience decision, not a security mechanism.

Never weaken, bypass, or replace backend authorization to make a UI feature easier to implement.

## 11. Responsive Design and Accessibility

The application should primarily target desktop users while remaining usable on tablets and mobile devices.

Requirements:
- Responsive sidebar.
- Responsive tables.
- Horizontally scrollable Kanban boards where appropriate.
- Mobile-friendly card details.
- Accessible buttons and form controls.
- Keyboard-friendly interactions.
- Clear validation and error messages.
- Appropriate contrast and readable typography.

## 12. Phase 5 Implementation Approach

Phase 5 focuses exclusively on the Blazor frontend and user experience.

The backend architecture and previously agreed authorization rules should be treated as established.

Do not unnecessarily modify:
- Domain entities.
- Database schema.
- Existing migrations.
- Backend authorization policies.
- Existing API contracts.
- Authentication architecture.

If a UI requirement genuinely cannot be supported by the current backend, explain the limitation and propose the smallest necessary change before modifying backend code.

Follow an incremental implementation approach:

1. Review the existing Blazor project structure.
2. Understand the existing authentication and API integration.
3. Finalize the UI architecture and navigation.
4. Establish the shared application layout.
5. Establish reusable design components.
6. Implement role-specific dashboards.
7. Implement workspace pages.
8. Implement project pages.
9. Implement board and task interfaces.
10. Integrate all pages with existing APIs.
11. Verify role-specific navigation, permissions, responsiveness, and functionality.

Do not attempt to implement the entire frontend in one task.

Each implementation prompt must define a clear scope and affected projects.

## 13. Mandatory Instructions for Copilot

Before starting any task:

1. Read and follow `.github/copilot-instructions.md`.
2. Read and follow these UI design instructions.
3. Inspect the existing implementation.
4. Understand previously implemented components and services.
5. Identify reusable functionality.
6. Explain the proposed approach before making substantial architectural changes.

Do not invent missing backend functionality.

Do not introduce unnecessary dependencies or UI libraries without evaluating the existing project and explaining why the dependency is needed.

Preserve existing functionality and established architectural decisions.

Before completing each task:
- Build all affected projects.
- Verify API integration.
- Verify role-specific navigation.
- Verify permission-aware UI behavior.
- Verify loading, error, and empty states.
- Verify responsive behavior where applicable.
- Report changed files and remaining issues.

These UI design and frontend development instructions are permanent standards for FlowBoard Phase 5 and subsequent frontend development.