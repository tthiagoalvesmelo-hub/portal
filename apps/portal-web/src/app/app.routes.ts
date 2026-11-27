import { Routes } from '@angular/router';
import { StatusPage } from './status/status-page';

// Tela única desta demanda: "Status da API" em /status (UX-0001 v1.1 §6, P-UX-04).
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'status' },
  { path: 'status', component: StatusPage, title: 'Status da API' },
  { path: '**', redirectTo: 'status' },
];
