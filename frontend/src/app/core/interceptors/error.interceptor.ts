import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      console.error('HTTP error:', error);

      let message: string;

      switch (error.status) {
        case 0:
          message = 'Cannot connect to server. Is the backend running?';
          break;
        case 404:
          message = 'Resource not found.';
          break;
        case 500:
          message = 'Server error, please try again.';
          break;
        default:
          message = 'An unexpected error occurred.';
      }

      return throwError(() => new Error(message));
    })
  );
};
