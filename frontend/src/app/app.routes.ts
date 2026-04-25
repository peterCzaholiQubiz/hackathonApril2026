import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./features/dashboard/dashboard.component').then(
        (m) => m.DashboardComponent
      ),
  },
  {
    path: 'customers',
    loadComponent: () =>
      import('./features/customer-list/customer-list.component').then(
        (m) => m.CustomerListComponent
      ),
  },
  {
    path: 'customers/:id',
    loadComponent: () =>
      import('./features/customer-detail/customer-detail.component').then(
        (m) => m.CustomerDetailComponent
      ),
  },
  {
    path: 'risk-groups',
    loadComponent: () =>
      import('./features/risk-groups/risk-groups.component').then(
        (m) => m.RiskGroupsComponent
      ),
  },
  {
    path: 'test-data',
    loadComponent: () =>
      import('./features/test-data/test-data.component').then(
        (m) => m.TestDataComponent
      ),
  },
  {
    path: 'meter-reads',
    loadComponent: () =>
      import('./features/meter-reads/meter-reads.component').then(
        (m) => m.MeterReadsComponent
      ),
  },
  {
    path: '**',
    redirectTo: '',
  },
];
