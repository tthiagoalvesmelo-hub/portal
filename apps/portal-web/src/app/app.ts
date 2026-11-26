import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

// Esqueleto D6 (ADR-0001 v2.0), DEM-2026-001 / TASK-0008.
// A tela de status (US-0002) entra na implementação.
@Component({
  imports: [RouterOutlet],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {}
