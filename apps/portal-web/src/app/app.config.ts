import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { API_BASE_URL, readDefinedApiBaseUrl } from './config/api-base-url';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    // PORTAL_API_BASE_URL lido uma única vez (ADR-0001 v2.0 D7); inválido ⇒ null ⇒ estado de erro.
    { provide: API_BASE_URL, useFactory: readDefinedApiBaseUrl },
  ],
};
