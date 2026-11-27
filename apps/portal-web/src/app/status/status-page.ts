import {
  ChangeDetectionStrategy,
  PendingTasks,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { HealthClient } from './health-client';
import { HealthView, LOADING, STATUS_TEXTS, healthyAnnouncement } from './health-status';

/**
 * Tela "Status da API" (US-0002; UX-0001 v1.1). Três estados exclusivos; o bloco de cada estado é criado
 * e destruído pelo bloco @if / @else if / @else do template, então valores anteriores saem do DOM durante o carregamento (CA-04).
 * Dados só por interpolação de texto, nunca innerHTML (CA-06).
 */
@Component({
  selector: 'app-status-page',
  templateUrl: './status-page.html',
  styleUrl: './status-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatusPage implements OnInit {
  private readonly client = inject(HealthClient);
  private readonly pendingTasks = inject(PendingTasks);
  private readonly inFlight = signal(false);

  protected readonly texts = STATUS_TEXTS;
  protected readonly view = signal<HealthView>(LOADING);
  protected readonly healthy = computed(() => {
    const view = this.view();
    return view.kind === 'saudavel' ? view : null;
  });
  protected readonly busy = computed(() => this.inFlight() || this.view().kind === 'carregando');
  protected readonly politeAnnouncement = computed(() => {
    const view = this.view();
    if (view.kind === 'carregando') {
      return STATUS_TEXTS.loadingAnnouncement;
    }
    return view.kind === 'saudavel' ? healthyAnnouncement(view.version) : '';
  });

  ngOnInit(): void {
    void this.refresh();
  }

  /** Ativação do botão (clique; Enter/Espaço chegam como clique no <button> nativo). Ignorada durante a consulta. */
  protected onRefresh(): void {
    if (this.inFlight()) {
      return;
    }
    void this.refresh();
  }

  private async refresh(): Promise<void> {
    this.inFlight.set(true);
    this.view.set(LOADING);
    // Consulta registrada como tarefa pendente: a aplicação só fica "estável" quando o estado final é aplicado.
    const done = this.pendingTasks.add();
    try {
      this.view.set(await this.client.check());
    } finally {
      this.inFlight.set(false);
      done();
    }
  }
}
