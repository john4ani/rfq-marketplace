import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { authInterceptor } from './core/auth/auth.interceptor';
import { AuthService } from './core/auth/auth.service';
import { backendAvailabilityInterceptor } from './core/connectivity/backend-availability.interceptor';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([backendAvailabilityInterceptor, authInterceptor])),
    // Recover the session eagerly without holding the public shell behind the API.
    // Protected routes await this same bounded initialization in their guards.
    provideAppInitializer(() => { void inject(AuthService).initialize(); })
  ]
};
