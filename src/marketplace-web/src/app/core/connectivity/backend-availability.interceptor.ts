import { HttpErrorResponse, HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, tap, throwError } from 'rxjs';
import { BackendAvailabilityService } from './backend-availability.service';

const unavailableStatuses = new Set([0, 502, 503, 504]);

function isUnavailable(error: HttpErrorResponse): boolean {
  if (unavailableStatuses.has(error.status))
    return true;

  // Vite represents a refused development proxy connection as an empty 500 response.
  return error.status === 500 && (error.error === null || error.error === '');
}

export const backendAvailabilityInterceptor: HttpInterceptorFn = (request, next) => {
  const backend = inject(BackendAvailabilityService);

  if (backend.shouldBlockRequest(request.url)) {
    return throwError(() => new HttpErrorResponse({
      status: 0,
      statusText: 'Backend temporarily unavailable',
      url: request.url
    }));
  }

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
