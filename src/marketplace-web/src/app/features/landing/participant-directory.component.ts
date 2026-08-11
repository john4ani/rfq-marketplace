import { ChangeDetectionStrategy, Component, OnDestroy, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { LiveMarketService, LiveParticipant, LiveMarketSnapshot } from '../../core/marketplace/live-market.service';

@Component({
  selector: 'app-participant-directory',
  imports: [RouterLink],
  template: `<section class="directory-shell">
    <a class="back" routerLink="/">← Înapoi la Piața Live</a>
    <header>
      <div><p class="eyebrow">Comunitate</p><h1>Directorul participanților</h1>
        <p>Descoperă cumpărători și furnizori din piață, cu prioritate pentru cei activi și apropiați.</p></div>
      <div class="pulse"><span aria-hidden="true"></span><div>
        <strong>{{ participantCount() }} {{ participantCount() === 1 ? 'participant' : 'participanți' }}</strong>
        <small>{{ onlineCount() }} online acum</small>
      </div></div>
    </header>

    @if (snapshot()?.isLocationFiltered) {
      <p class="location-note">Rezultatele sunt prioritizate pentru {{ snapshot()?.filterCity }}, {{ snapshot()?.filterCounty }} și zona apropiată.</p>
    }

    <section class="filters" aria-label="Filtre participanți">
      <label>Caută după nume sau localitate
        <input type="search" [value]="search()" (input)="search.set($any($event.target).value)" placeholder="Exemplu: Cluj sau Construct">
      </label>
      <label class="checkbox"><input type="checkbox" [checked]="includeOffline()" (change)="includeOffline.set($any($event.target).checked)">
        Include și participanții offline</label>
    </section>

    @if (loading()) {
      <div class="skeletons" role="status" aria-label="Se încarcă participanții"><span></span><span></span><span></span></div>
    } @else if (error()) {
      <section class="state error" role="alert"><div><strong>Directorul nu este disponibil momentan.</strong><span>{{ error() }}</span></div><button type="button" (click)="retry()">Încearcă din nou</button></section>
    } @else {
      <section class="group">
        <div class="group-title"><div><p class="eyebrow">Cerere</p><h2>Cumpărători</h2></div><span>{{ buyers().length }} rezultate</span></div>
        <div class="participant-grid">
          @for (participant of buyers().slice(0, 24); track participant.id) {
            <article class="participant"><span class="avatar" aria-hidden="true">{{ initials(participant.name) }}</span>
              <div><strong>{{ participant.name }}</strong><small>{{ participant.city }}, {{ participant.county }}</small></div>
               <span class="status" [class.online]="participant.isOnline">{{ participant.isOnline ? 'Online' : 'Offline' }}</span>
            </article>
          } @empty { <p class="state">Nu am găsit cumpărători pentru filtrele selectate.</p> }
        </div>
      </section>

      <section class="group">
        <div class="group-title"><div><p class="eyebrow">Furnizare</p><h2>Furnizori</h2></div><span>{{ suppliers().length }} rezultate</span></div>
        <div class="participant-grid">
          @for (participant of suppliers().slice(0, 24); track participant.id) {
            <article class="participant"><span class="avatar supplier" aria-hidden="true">{{ initials(participant.name) }}</span>
              <div><strong>{{ participant.name }}</strong><small>{{ participant.city }}, {{ participant.county }}</small></div>
              @if(participant.companyVerified){<small>✓ Companie verificată</small>}
              <span class="status" [class.online]="participant.isOnline">{{ participant.isOnline ? 'Online' : 'Offline' }}</span>
            </article>
          } @empty { <p class="state">Nu am găsit furnizori pentru filtrele selectate.</p> }
        </div>
      </section>
    }
  </section>`,
  styleUrl: './participant-directory.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ParticipantDirectoryComponent implements OnDestroy {
  private readonly market = inject(LiveMarketService);
  private readonly auth = inject(AuthService);
  readonly snapshot = signal<LiveMarketSnapshot | null>(null);
  readonly search = signal('');
  readonly includeOffline = signal(false);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly filtered = computed(() => {
    const query = this.search().trim().toLocaleLowerCase('ro');
    return (this.snapshot()?.participants ?? [])
      .filter(participant => this.includeOffline() || participant.isOnline)
      .filter(participant => !query || `${participant.name} ${participant.city} ${participant.county}`.toLocaleLowerCase('ro').includes(query))
      .sort((a, b) => Number(b.isOnline) - Number(a.isOnline) || Number(b.isSameCity) - Number(a.isSameCity) || a.name.localeCompare(b.name));
  });
  readonly buyers = computed(() => this.filtered().filter(participant => participant.profileType === 'Buyer'));
  readonly suppliers = computed(() => this.filtered().filter(participant => participant.profileType === 'Supplier'));
  readonly participantCount = computed(() => new Set(this.filtered().map(participant => participant.id)).size);
  readonly onlineCount = computed(() => new Set(this.filtered()
    .filter(participant => participant.isOnline).map(participant => participant.id)).size);
  private readonly refreshTimer: ReturnType<typeof setInterval>;

  constructor() {
    this.load();
    this.refreshTimer = setInterval(() => this.load(false), 30_000);
  }

  ngOnDestroy(): void { clearInterval(this.refreshTimer); }
  initials(name: string): string { return name.split(/\s+/).slice(0, 2).map(part => part[0]).join('').toUpperCase(); }
  retry(): void { this.load(); }

  private load(showLoading = true): void {
    if (showLoading) this.loading.set(true);
    this.market.getSnapshot(this.auth.session()?.accountType).subscribe({
      next: value => { this.snapshot.set(value); this.loading.set(false); this.error.set(''); },
      error: () => { this.loading.set(false); this.error.set('Directorul nu a putut fi încărcat. Încearcă din nou.'); }
    });
  }
}
