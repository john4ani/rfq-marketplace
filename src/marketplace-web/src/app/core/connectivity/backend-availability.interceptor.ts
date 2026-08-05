import { HttpErrorResponse, HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, tap, throwError } from 'rxjs';
import { BackendAvailabilityService } from './backend-availability.service';

const unavailableStatuses = new Set([0, 502, 503, 504]);

function isUnavailable(error: HttpErrorResponse): boolean {
  if (error.status === 503 && isApplicationProblem(error))
    return false;

  if (unavailableStatuses.has(error.status))
    return true;

  // Vite represents a refused development proxy connection as an empty 500 response.
  return error.status === 500 && (error.error === null || error.error === '');
}

function isApplicationProblem(error: HttpErrorResponse): boolean {
  const contentType = error.headers.get('content-type')?.toLowerCase() ?? '';
  if (contentType.includes('application/problem+json'))
    return true;

  return typeof error.error === 'object'
    && error.error !== null
    && error.error.status === 503;
}

export const backendAvailabilityInterceptor: HttpInterceptorFn = (request, next) => {
  const backend = inject(BackendAvailabilityService);

  return next(request).pipe(
    tap(event => {
      if (event instanceof HttpResponse)
        backend.markAvailable();
    }),
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        if (isUnavailable(error))
          backend.markUnavailable();
        else
          backend.markAvailable();
      }
      return throwError(() => error);
    })
  );
};
