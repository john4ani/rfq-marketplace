import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./features/landing/live-market.component').then(m => m.LiveMarketComponent) },
  { path: 'sign-in', loadComponent: () => import('./features/auth/sign-in.component').then(m => m.SignInComponent) },
  { path: 'register', loadComponent: () => import('./features/auth/register.component').then(m => m.RegisterComponent) },
  { path: 'dashboard', canActivate: [authGuard], loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent) },
  { path: 'open-requests', canActivate: [authGuard], loadComponent: () => import('./features/rfqs/open-rfq-list.component').then(m => m.OpenRfqListComponent) },
  { path: 'open-requests/:rfqId', canActivate: [authGuard], loadComponent: () => import('./features/rfqs/open-rfq-detail.component').then(m => m.OpenRfqDetailComponent) },
  { path: 'rfqs/new', canActivate: [authGuard], loadComponent: () => import('./features/rfqs/rfq-editor.component').then(m => m.RfqEditorComponent) },
  { path: 'rfqs/:rfqId/edit', canActivate: [authGuard], loadComponent: () => import('./features/rfqs/rfq-editor.component').then(m => m.RfqEditorComponent) },
  { path: '**', redirectTo: '' }
];
