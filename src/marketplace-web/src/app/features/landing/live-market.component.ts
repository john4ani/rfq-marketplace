import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnDestroy, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { LiveMarketService, LiveParticipant } from '../../core/marketplace/live-market.service';

@Component({
  selector: 'app-live-market',
  imports: [RouterLink, NgTemplateOutlet],
  template: `<section class="live-hero">
    <div>
      <p class="eyebrow">Piața materialelor de construcții</p>
      <h1>Piața Live</h1>
      <p class="hero-copy">Vezi cine face parte din piață și cine este disponibil chiar acum.</p>
      <div class="hero-actions">
        @if (auth.isAuthenticated()) {
          <a class="primary-action" routerLink="/dashboard">Mergi la contul meu</a>
        } @else {
          <a class="primary-action" routerLink="/register">Intră în piață</a>
          <a class="secondary-action" routerLink="/sign-in">Autentificare</a>
        }
      </div>
    </div>
    <div class="market-stats" aria-label="Statistici Piața Live">
      <div><strong>{{ buyers().length }}</strong><span>Cumpărători</span></div>
      <div><strong>{{ suppliers().length }}</strong><span>Furnizori</span></div>
      <div class="online-stat"><strong>{{ onlineCount() }}</strong><span>Online acum</span></div>
    </div>
  </section>

  <section class="market-toolbar" aria-label="Filtrare participanți">
    <label class="search-label">Caută în piață
      <input #searchInput type="search" placeholder="Nume cumpărător sau furnizor" (input)="search.set(searchInput.value)">
    </label>
    <label class="online-filter"><input #onlineInput type="checkbox" (change)="onlineOnly.set(onlineInput.checked)">Arată doar participanții online</label>
    @if (lastUpdated()) { <small>Actualizat la {{ lastUpdated() }}</small> }
  </section>
  @if (locationFiltered()) {
    <p class="location-filter" role="status"><strong>Zona ta:</strong> {{ filterCity() }}, {{ filterCounty() }}.
      Afișăm mai întâi participanții din orașul tău, apoi pe cei din județ.
      <span>Preferință profil: {{ approximateRadiusKm() }} km.</span></p>
  } @else if (auth.isAuthenticated()) {
    <p class="location-filter warning">Completează localitatea și județul profilului activ pentru a vedea piața din apropiere.</p>
  }

  @if (loading()) {
    <section class="market-state" role="status"><span class="pulse-dot"></span> Se încarcă piața…</section>
  } @else if (error()) {
    <section class="market-state error" role="alert">{{ error() }} <button type="button" (click)="load()">Reîncearcă</button></section>
  } @else {
    <section class="participant-columns">
      <section class="participant-group" aria-labelledby="buyers-title">
        <div class="group-heading"><div><p class="eyebrow">Cumpărători</p><h2 id="buyers-title">Cei care caută oferte</h2></div>
          <span>{{ onlineIn(filteredBuyers()) }} online</span></div>
        <div class="participant-list">
          @for (participant of filteredBuyers(); track participant.id) { <ng-container *ngTemplateOutlet="participantCard; context: {$implicit: participant}" /> }
          @empty { <p class="empty-state">Nu există cumpărători pentru filtrul ales.</p> }
        </div>
      </section>
      <section class="participant-group" aria-labelledby="suppliers-title">
        <div class="group-heading"><div><p class="eyebrow">Furnizori</p><h2 id="suppliers-title">Cei care pot răspunde</h2></div>
          <span>{{ onlineIn(filteredSuppliers()) }} online</span></div>
        <div class="participant-list">
          @for (participant of filteredSuppliers(); track participant.id) { <ng-container *ngTemplateOutlet="participantCard; context: {$implicit: participant}" /> }
          @empty { <p class="empty-state">Nu există furnizori pentru filtrul ales.</p> }
        </div>
      </section>
    </section>
  }

  <ng-template #participantCard let-participant>
    <article class="participant-card" [class.online]="participant.isOnline">
      <span class="avatar" aria-hidden="true">{{ initials(participant.name) }}</span>
      <div><strong>{{ participant.name }}</strong><small>{{ participant.profileType === 'Buyer' ? 'Cumpărător' : 'Furnizor' }} · {{ participant.city }}, {{ participant.county }}
        @if (participant.isSameCity) { <em>În orașul tău</em> }</small></div>
      <span class="status" [class.online]="participant.isOnline"><i></i>{{ participant.isOnline ? 'Online' : 'Offline' }}</span>
    </article>
  </ng-template>`,
  styleUrl: './live-market.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LiveMarketComponent implements OnDestroy {
  readonly auth = inject(AuthService);
  private readonly market = inject(LiveMarketService);
  readonly participants = signal<LiveParticipant[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly lastUpdated = signal('');
  readonly search = signal('');
  readonly onlineOnly = signal(false);
  readonly locationFiltered = signal(false);
  readonly filterCity = signal<string|null>(null);
  readonly filterCounty = signal<string|null>(null);
  readonly approximateRadiusKm = signal<number|null>(null);
  readonly buyers = computed(() => this.participants().filter(participant => participant.profileType === 'Buyer'));
  readonly suppliers = computed(() => this.participants().filter(participant => participant.profileType === 'Supplier'));
  readonly onlineCount = computed(() => this.participants().filter(participant => participant.isOnline).length);
  readonly filteredBuyers = computed(() => this.filter(this.buyers()));
  readonly filteredSuppliers = computed(() => this.filter(this.suppliers()));
  private readonly refreshTimer: ReturnType<typeof setInterval>;

  constructor() {
    this.load();
    this.refreshTimer = setInterval(() => this.load(false), 15_000);
  }
  ngOnDestroy(): void { clearInterval(this.refreshTimer); }
  load(showLoading = true): void {
    if (showLoading) this.loading.set(true);
    this.error.set('');
    this.market.getSnapshot(this.auth.session()?.accountType).subscribe({
      next: snapshot => {
        this.participants.set(snapshot.participants);
        this.locationFiltered.set(snapshot.isLocationFiltered);
        this.filterCity.set(snapshot.filterCity);
        this.filterCounty.set(snapshot.filterCounty);
        this.approximateRadiusKm.set(snapshot.approximateRadiusKm);
        this.lastUpdated.set(new Date(snapshot.generatedAt).toLocaleTimeString('ro-RO', { hour: '2-digit', minute: '2-digit' }));
        this.loading.set(false);
      },
      error: () => { this.loading.set(false); this.error.set('Piața Live nu poate fi încărcată. Verifică dacă API-ul rulează.'); }
    });
  }
  onlineIn(values: LiveParticipant[]): number { return values.filter(participant => participant.isOnline).length; }
  initials(name: string): string { return name.split(/\s+/).slice(0, 2).map(part => part[0]?.toUpperCase()).join(''); }
  private filter(values: LiveParticipant[]): LiveParticipant[] {
    const term = this.search().trim().toLocaleLowerCase('ro-RO');
    return values.filter(participant => (!this.onlineOnly() || participant.isOnline) && (!term || participant.name.toLocaleLowerCase('ro-RO').includes(term)));
  }
}
