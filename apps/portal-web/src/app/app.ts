import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

// Casca do Portal Web (ADR-0001 v2.0 D6), DEM-2026-001 / TASK-0008: cabeçalho provisório + rota.
@Component({
  imports: [RouterOutlet],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {}
