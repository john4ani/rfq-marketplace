import { Routes } from '@angular/router';
import { administratorGuard, authGuard, guestOnlyGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./features/landing/live-market.component').then(m => m.LiveMarketComponent) },
  { path: 'participants', loadComponent: () => import('./features/landing/participant-directory.component').then(m => m.ParticipantDirectoryComponent) },
  { path: 'sign-in', loadComponent: () => import('./features/auth/sign-in.component').then(m => m.SignInComponent) },
  { path: 'register', canActivate: [guestOnlyGuard], loadComponent: () => import('./features/auth/register.component').then(m => m.RegisterComponent) },
  { path: 'forgot-password', loadComponent: () => import('./features/auth/forgot-password.component').then(m => m.ForgotPasswordComponent) },
  { path: 'reset-password', loadComponent: () => import('./features/auth/reset-password.component').then(m => m.ResetPasswordComponent) },
  { path: 'verify-email', loadComponent: () => import('./features/auth/verify-email.component').then(m => m.VerifyEmailComponent) },
  { path: 'supplier-invitations/:token', loadComponent: () => import('./features/supplier-discovery/supplier-invitation.component').then(m => m.SupplierInvitationComponent) },
  { path: 'admin/supplier-discovery', canActivate: [administratorGuard], loadComponent: () => import('./features/supplier-discovery/supplier-discovery-admin.component').then(m => m.SupplierDiscoveryAdminComponent) },
  { path: 'admin/catalog', canActivate: [administratorGuard], loadComponent: () => import('./features/admin/category-catalog.component').then(m => m.CategoryCatalogComponent) },
  { path: 'admin/trust', canActivate: [administratorGuard], loadComponent: () => import('./features/admin/trust-admin.component').then(m => m.TrustAdminComponent) },
  { path: 'dashboard', canActivate: [authGuard], loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent) },
  { path: 'open-requests', canActivate: [authGuard], loadComponent: () => import('./features/rfqs/open-rfq-list.component').then(m => m.OpenRfqListComponent) },
  { path: 'open-requests/:rfqId', canActivate: [authGuard], loadComponent: () => import('./features/rfqs/open-rfq-detail.component').then(m => m.OpenRfqDetailComponent) },
  { path: 'rfqs/new/advanced', canActivate: [authGuard], loadComponent: () => import('./features/rfqs/rfq-editor.component').then(m => m.RfqEditorComponent) },
  { path: 'rfqs/new', canActivate: [authGuard], loadComponent: () => import('./features/rfqs/assisted-intake.component').then(m => m.AssistedIntakeComponent) },
  { path: 'rfqs/:rfqId/edit', canActivate: [authGuard], loadComponent: () => import('./features/rfqs/rfq-editor.component').then(m => m.RfqEditorComponent) },
  { path: 'rfqs/:rfqId/offers', canActivate: [authGuard], loadComponent: () => import('./features/offers/buyer-offer-comparison.component').then(m => m.BuyerOfferComparisonComponent) },
  { path: 'offers/:offerId/conversation', canActivate: [authGuard], loadComponent: () => import('./features/messaging/conversation.component').then(m => m.ConversationComponent) },
  { path: '**', redirectTo: '' }
];
