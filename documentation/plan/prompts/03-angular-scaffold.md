# Prompt 03 — Angular Frontend Scaffold

**Agent**: `general-purpose`  
**Phase**: 1 — Foundation  
**Status**: DONE

---

Create the Angular frontend scaffold for the Customer Portfolio Thermometer.

Read documentation/plan.md (Frontend section) for the full structure.

1. Initialise the project:
   Run: ng new portfolio-thermometer --standalone --routing --style=scss
   Output directory: frontend/

2. Create all core services with typed interfaces matching the backend models:
   - frontend/src/app/core/services/portfolio.service.ts
   - frontend/src/app/core/services/customer.service.ts
   - frontend/src/app/core/services/risk.service.ts
   - frontend/src/app/core/services/import.service.ts

3. Create TypeScript models in frontend/src/app/core/models/:
   - customer.model.ts
   - risk-score.model.ts
   - risk-explanation.model.ts
   - suggested-action.model.ts
   - portfolio-snapshot.model.ts
   - api-response.model.ts  (consistent envelope: { success, data, error, meta })

4. Configure routing in app.routes.ts with 4 routes:
   - /                  -> DashboardComponent
   - /customers         -> CustomerListComponent
   - /customers/:id     -> CustomerDetailComponent
   - /risk-groups       -> RiskGroupsComponent

5. Create stub components (empty shell, no logic yet) for all 4 feature areas:
   - features/dashboard/dashboard.component.ts
   - features/customer-list/customer-list.component.ts
   - features/customer-detail/customer-detail.component.ts
   - features/risk-groups/risk-groups.component.ts

6. Create shared components (stubs):
   - shared/components/heat-badge/heat-badge.component.ts
   - shared/components/risk-gauge/risk-gauge.component.ts
   - shared/components/score-bar/score-bar.component.ts
   - shared/components/loading-skeleton/loading-skeleton.component.ts

7. Create shared pipes:
   - shared/pipes/heat-color.pipe.ts  (maps 'green'/'yellow'/'red' to CSS color vars)
   - shared/pipes/risk-label.pipe.ts  (maps score 0-100 to 'Low'/'Medium'/'High')

8. Create frontend/src/app/core/interceptors/error.interceptor.ts
   (catches HTTP errors, logs them, re-throws user-friendly messages)

9. Set environments:
   - environments/environment.ts: { apiUrl: 'http://localhost:8080' }
   - environments/environment.development.ts: same

10. Create frontend/Dockerfile for Angular dev server (node:20-alpine, ng serve --host 0.0.0.0)

Reference: documentation/plan.md (Frontend section)
