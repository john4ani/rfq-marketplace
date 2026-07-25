import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./features/landing/live-market.component').then(m => m.LiveMarketComponent) },
  { path: 'participants', loadComponent: () => import('./features/landing/participant-directory.component').then(m => m.ParticipantDirectoryComponent) },
  { path: 'sign-in', loadComponent: () => import('./features/auth/sign-in.component').then(m => m.SignInComponent) },
  { path: 'register', loadComponent: () => import('./features/auth/register.component').then(m => m.RegisterComponent) },
  { path: 'forgot-password', loadComponent: () => import('./features/auth/forgot-password.component').then(m => m.ForgotPasswordComponent) },
  { path: 'reset-password', loadComponent: () => import('./features/auth/reset-password.component').then(m => m.ResetPasswordComponent) },
  { path: 'dashboard', canActivate: [authGuard], loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent) },
  { path: 'open-requests', canActivate: [authGuard], loadComponent: () => import('./features/rfqs/open-rfq-list.component').then(m => m.OpenRfqListComponent) },
  { path: 'open-requests/:rfqId', canActivate: [authGuard], loadComponent: () => import('./features/rfqs/open-rfq-detail.component').then(m => m.OpenRfqDetailComponent) },
  { path: 'rfqs/new', canActivate: [authGuard], loadComponent: () => import('./features/rfqs/rfq-editor.component').then(m => m.RfqEditorComponent) },
  { path: 'rfqs/:rfqId/edit', canActivate: [authGuard], loadComponent: () => import('./features/rfqs/rfq-editor.component').then(m => m.RfqEditorComponent) },
  { path: 'rfqs/:rfqId/offers', canActivate: [authGuard], loadComponent: () => import('./features/offers/buyer-offer-comparison.component').then(m => m.BuyerOfferComparisonComponent) },
  { path: 'offers/:offerId/conversation', canActivate: [authGuard], loadComponent: () => import('./features/messaging/conversation.component').then(m => m.ConversationComponent) },
  { path: '**', redirectTo: '' }
];
