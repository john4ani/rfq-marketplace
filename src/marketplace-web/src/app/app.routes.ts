import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: 'sign-in', loadComponent: () => import('./features/auth/sign-in.component').then(m => m.SignInComponent) },
  { path: 'register', loadComponent: () => import('./features/auth/register.component').then(m => m.RegisterComponent) },
  { path: 'dashboard', canActivate: [authGuard], loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent) },
  { path: '', pathMatch: 'full', redirectTo: 'sign-in' },
  { path: '**', redirectTo: 'sign-in' }
];
