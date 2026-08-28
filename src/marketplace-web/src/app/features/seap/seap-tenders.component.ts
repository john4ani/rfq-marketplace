import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { timer } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { FeaturesService } from '../../core/features/features.service';
import { CompanyVaultService, SpecificationBidResult, PIPELINE_STAGE_LABELS } from '../../core/companies/company-vault.service';
import { ProfileService } from '../../core/profiles/profile.service';
import { SeapService, SeapTender } from '../../core/seap/seap.service';
import { ROMANIAN_COUNTIES } from '../../shared/romanian-counties';
import { normalizeCounty, normalizeCpvPrefix } from '../../shared/seap-filters';

@Component({
  selector: 'app-seap-tenders',
  imports: [RouterLink, DatePipe, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`:host{display:block}.page{display:grid;gap:1.2rem;max-width:76rem}.back-link{display:inline-flex;align-items:center;gap:.4rem;width:fit-content;color:var(--forest);font-weight:750;text-decoration:none;font-size:.92rem}.eyebrow{margin:0;color:var(--forest-light);font-size:.76rem;font-weight:800;letter-spacing:.1em;text-transform:uppercase}h1{margin:.15rem 0;font-size:clamp(1.9rem,4vw,2.8rem);letter-spacing:-.04em}.page>p{margin:0;color:var(--muted);line-height:1.5}.card,.state,.filters{padding:1.15rem 1.25rem;border:1px solid var(--border);border-radius:1rem;background:var(--surface);box-shadow:var(--shadow-soft)}.card{display:flex;justify-content:space-between;gap:1.25rem;align-items:center;text-decoration:none;color:inherit;transition:border-color .15s}.card:hover{border-color:color-mix(in srgb,var(--forest) 45%,var(--border))}.card h2{margin:0;font-size:1.1rem}.card p{margin:.2rem 0 0;color:var(--muted);font-size:.9rem}.pill{display:inline-flex;align-items:center;padding:.3rem .72rem;border-radius:999px;font-size:.78rem;font-weight:800}.pill.ready{color:#176b52;background:#e3f2ea}.pill.done{color:var(--forest);background:color-mix(in srgb,var(--sand) 60%,white)}.arrow{flex:0 0 auto;color:var(--forest);font-weight:800}.state{justify-items:center;padding:2rem;text-align:center;color:var(--muted)}.state strong{color:var(--ink)}.state button{min-height:2.6rem;padding:.6rem 1.05rem;border:0;border-radius:.6rem;color:#fff;background:var(--forest);font:inherit;font-weight:800;cursor:pointer}.filters{display:grid;gap:.7rem}.filters-header{display:flex;align-items:baseline;gap:.6rem;flex-wrap:wrap}.filters-header strong{font-size:.95rem}.filters-header .company{color:var(--forest);font-weight:750;font-size:.85rem}.checks{display:flex;flex-wrap:wrap;gap:.5rem}.check{display:inline-flex;align-items:center;gap:.4rem;padding:.42rem .8rem;border:1px solid var(--border);border-radius:999px;background:var(--warm-white);font-size:.86rem;font-weight:650;cursor:pointer}.check input{accent-color:var(--forest)}.check.radius{border-style:dashed}.check:has(input:disabled){opacity:.6;cursor:not-allowed}.radius-hint{display:block;margin-top:.25rem;font-size:.74rem;color:var(--muted);font-weight:500}.hint{margin:0;color:var(--muted);font-size:.82rem;line-height:1.45}.upload{display:flex;gap:.7rem;flex-wrap:wrap;align-items:center}.upload input{flex:1 1 18rem}.upload button{min-height:2.6rem;padding:.6rem 1.05rem;border:0;border-radius:.6rem;color:#fff;background:var(--forest);font:inherit;font-weight:800;cursor:pointer}.upload button:disabled{opacity:.55;cursor:not-allowed}.spec-result{display:grid;gap:.6rem;padding:.85rem .95rem;border:1px solid var(--border);border-radius:.8rem;background:var(--warm-white)}.spec-result strong{font-size:.95rem}.downloads{display:flex;flex-wrap:wrap;gap:.6rem}.downloads button{display:inline-flex;padding:.5rem .85rem;border:0;border-radius:.6rem;color:var(--forest);background:color-mix(in srgb,var(--sand) 45%,white);font:inherit;font-weight:800;text-decoration:none;font-size:.85rem;cursor:pointer}.spec-result button{min-height:2.3rem;padding:.4rem .8rem;border:0;border-radius:.6rem;color:var(--forest);background:var(--sand);font:inherit;font-weight:800;cursor:pointer;width:fit-content}.pipeline-progress{display:grid;gap:.45rem;padding:.75rem .95rem;border:1px solid var(--border);border-radius:.7rem;background:var(--warm-white)}.pipeline-progress p{margin:0;color:var(--forest);font-size:.86rem;font-weight:650}.progress-track{height:.5rem;border-radius:999px;background:color-mix(in srgb,var(--sand) 70%,white);overflow:hidden}.progress-bar{height:100%;width:100%;border-radius:999px;background:var(--forest-light);transform-origin:0 50%;animation:pipeline-slide 1.6s ease-in-out infinite}@keyframes pipeline-slide{0%{transform:scaleX(.15)}50%{transform:scaleX(.65)}100%{transform:scaleX(.15)}}.note{padding:.75rem .95rem;border-radius:.7rem;line-height:1.5;font-size:.86rem}.note:not(.error){background:#edf6f1;color:#315947}.note.error{background:#fff0f0;color:#8b2222}.no-company{display:flex;justify-content:space-between;gap:1.1rem;align-items:center;padding:1.05rem 1.25rem;border:1px solid color-mix(in srgb,var(--warm-amber) 55%,var(--border));border-radius:1rem;background:color-mix(in srgb,var(--warm-amber) 12%,var(--surface));box-shadow:var(--shadow-soft)}.no-company strong{display:block;font-size:.98rem}.no-company p{margin:.25rem 0 0;color:var(--muted);font-size:.88rem;line-height:1.5}.no-company a{flex:0 0 auto;display:inline-flex;padding:.6rem 1rem;border-radius:.6rem;color:#fff;background:var(--forest);font-weight:800;text-decoration:none}`, `:host{display:block}.advanced{margin-top:1rem;display:grid;gap:.7rem}.advanced-header{display:flex;align-items:baseline;justify-content:space-between;gap:.6rem;flex-wrap:wrap}.advanced-header strong{font-size:.95rem}.advanced-header a{color:var(--forest);font-weight:750;font-size:.85rem}.advanced-editor{display:grid;gap:.8rem}.advanced-field{display:grid;gap:.45rem}.advanced-field label{font-weight:750;font-size:.86rem}.chips{display:flex;flex-wrap:wrap;gap:.45rem}.chip{display:inline-flex;align-items:center;gap:.35rem;padding:.32rem .6rem;border-radius:999px;color:var(--forest);background:color-mix(in srgb,var(--sand) 60%,white);font-size:.8rem;font-weight:750}.chip-remove{border:0;background:transparent;color:inherit;font:inherit;font-weight:900;cursor:pointer;padding:.05rem .15rem;line-height:1}.chip-empty{color:var(--muted);font-size:.82rem}.chip-input{display:flex;gap:.5rem;align-items:center}.chip-input input,.chip-input select{flex:1;min-width:0;padding:.5rem .7rem;border:1px solid var(--border);border-radius:.55rem;font:inherit;background:#fff}.chip-input button,.advanced-actions .save{min-height:2.4rem;padding:.45rem .9rem;border:0;border-radius:.55rem;color:#fff;background:var(--forest);font:inherit;font-weight:800;cursor:pointer}.chip-input button:disabled,.advanced-actions .save:disabled{opacity:.55;cursor:not-allowed}.advanced-actions{display:flex;align-items:center;gap:.8rem;flex-wrap:wrap}.advanced-actions .save{min-height:2.6rem}.advanced-actions .hint{color:var(--muted);font-size:.82rem}.filter-summary{margin:.5rem 0 0;padding:.55rem .8rem;border-radius:.6rem;color:#7a5206;background:#faf0d8;font-size:.8rem;line-height:1.45}`],
  template: `<section class="page">
    <a class="back-link" [routerLink]="features.marketplaceEnabled() ? ['/dashboard'] : ['/account']">← Înapoi</a>
    <header><p class="eyebrow">Achiziții publice</p><h1>Anunțuri SEAP</h1>
      <p>Oportunități publice din e-licitatie.ro, cu ofertare automată asistată de AI.</p></header>

    @if (isAdmin()) {
      <section class="filters" aria-label="Caiet de sarcini — test admin">
        <div class="filters-header"><strong>Caiet de sarcini — pipeline auto-bid</strong><span class="company">test admin</span></div>
        @if (companyId(); as companyId) {
          <div class="upload"><input type="file" accept="application/pdf" (change)="onSpecFileSelected($any($event.target).files?.[0])" aria-label="Alege PDF-ul Caietului de sarcini">
            <button type="button" [disabled]="!specFile() || specBusy()" (click)="uploadSpecification()">{{ specBusy() ? 'Se procesează…' : 'Încarcă Caietul de sarcini și generează oferta' }}</button></div>
          <p class="hint">Upload PDF → Azure Document Intelligence → Markdown → propunere tehnică (.docx) + listă de cantități (.xlsx). Procesarea durează de obicei 30–60 de secunde.</p>
          @if (specBusy()) {
            <div class="pipeline-progress" role="status" aria-live="polite">
              <div class="progress-track"><div class="progress-bar"></div></div>
              <p>{{ specStageLabel() }}</p>
            </div>
          }
          @if (specMessage()) { <p class="note" [class.error]="specError()" role="alert">{{ specMessage() }}</p> }
          @if (specResult(); as result) {
            <div class="spec-result">
              <strong>{{ result.proposalTitle }}</strong>
              <div class="downloads">
                <button type="button" [disabled]="!acceptsSpecResponsibility()" (click)="downloadSpecFile('TechnicalProposal')">Descarcă propunerea tehnică (.docx)</button>
                <button type="button" [disabled]="!acceptsSpecResponsibility()" (click)="downloadSpecFile('BillOfQuantities')">Descarcă lista de cantități (.xlsx)</button>
              </div>
              <label class="acceptance"><input type="checkbox" [checked]="acceptsSpecResponsibility()" (change)="acceptsSpecResponsibility.set($any($event.target).checked)" />
                <span>Îmi asum responsabilitatea verificării și asumării conținutului înainte de semnarea electronică și depunerea în SEAP. CompaSpot este un instrument software de redactare asistată, nu consultanță juridică sau tehnică.</span></label>
              <button type="button" [disabled]="specBusy()" (click)="regenerateSpecification(companyId)">Regenerează propunerea</button>
            </div>
          }
        } @else {
          <p class="hint">Admin: pentru testul pipeline-ului ai nevoie de o firmă în cont. Creează/configurează firma din secțiunea „Firma mea".</p>
        }
      </section>
    }

    @if (profileLoaded()) {
      @if (!companyId()) {
        <section class="no-company" role="status">
          <div><strong>Înregistrează firma pe care o reprezinți</strong>
            <p>Ofertarea automată folosește datele firmei tale (Company Vault, categorii, județ). Adaugă firma ca să poți genera propuneri tehnice și devize pentru anunțurile SEAP.</p></div>
          <a routerLink="/account/company/new">Adaugă firma</a>
        </section>
      }
      <section class="filters" aria-label="Filtre anunțuri">
        <div class="filters-header"><strong>Filtre SEAP</strong>
          @if (companyName(); as name) { <span class="company">{{ name }}</span> }</div>
        <label class="check radius">
          <input type="checkbox" [checked]="radiusActive()" [disabled]="!county()" (change)="toggleRadius()" />
          {{ county() ? 'Doar anunțurile în raza de ' + radiusKm() + ' km de ' + county() : 'Filtru pe rază indisponibil — firma nu are județ configurat' }}
          <small class="radius-hint">Raza se editează din Cont → Firma → setările firmei.</small>
        </label>
        <div class="advanced">
          <div class="advanced-header"><strong>Filtre avansate (CPV + județe)</strong>
            <a routerLink="/seap/match">Configurează-ți potrivirea cu AI</a></div>
          @if (companyId(); as companyId) {
            <div class="advanced-editor">
              <div class="advanced-field"><label>Coduri CPV</label>
                <div class="chips">
                  @for (prefix of cpvPrefixes(); track prefix) {
                    <span class="chip">{{ prefix }}<button type="button" class="chip-remove" (click)="removeCpvPrefix(prefix)" [attr.aria-label]="'Șterge ' + prefix">×</button></span>
                  }
                  @if (cpvPrefixes().length === 0) { <span class="chip-empty">Niciun prefix — toate domeniile</span> }
                </div>
                <div class="chip-input"><input [value]="cpvInput()" (input)="cpvInput.set($any($event.target).value)" placeholder="ex. 45233000" aria-label="Adaugă prefix CPV">
                  <button type="button" (click)="addCpvPrefix()" [disabled]="!normalizeCpvPrefix(cpvInput())">Adaugă</button></div>
              </div>
              <div class="advanced-field"><label>Județe</label>
                <div class="chips">
                  @for (county of counties(); track county) {
                    <span class="chip">{{ county }}<button type="button" class="chip-remove" (click)="removeCounty(county)" [attr.aria-label]="'Șterge ' + county">×</button></span>
                  }
                  @if (counties().length === 0) { <span class="chip-empty">Toată țara</span> }
                </div>
                <div class="chip-input"><select [value]="countyDraft()" (change)="countyDraft.set($any($event.target).value)" aria-label="Alege județul de adăugat">
                    <option value="">Alege județul</option>@for (county of availableCounties(); track county) { <option [value]="county">{{ county }}</option> }</select>
                  <button type="button" (click)="addCounty()" [disabled]="!countyDraft()">Adaugă</button></div>
              </div>
              <div class="advanced-actions">
                <button type="button" class="save" [disabled]="advancedSaving()" (click)="saveAdvancedFilters()">{{ advancedSaving() ? 'Se salvează…' : 'Salvează filtrele' }}</button>
                <span class="hint">Salvate pe profilul firmei și folosite ca implicit la fiecare vizită.</span>
              </div>
              @if (advancedMessage()) { <p class="note" [class.error]="advancedError()" role="alert">{{ advancedMessage() }}</p> }
            </div>
          } @else {
            <p class="hint">Adaugă o firmă pentru a salva filtre avansate.</p>
          }
        </div>
      </section>
    }

    @if (loading()) { <section class="state" role="status">Se încarcă anunțurile…</section> }
    @else if (error()) { <section class="state" role="alert"><strong>Anunțurile nu au putut fi încărcate.</strong><button type="button" (click)="load()">Încearcă din nou</button></section> }
    @else if (tenders().length === 0) { <section class="state"><strong>Nu există anunțuri potrivite.</strong><p>Schimbă filtrele sau așteaptă importul următor de anunțuri.</p>@if (activeFilterSummary(); as summary) { <p class="filter-summary">Filtre active: {{ summary }}</p> }</section> }
    @else {
      @for (tender of tenders(); track tender.id) {
        <a class="card" [routerLink]="['/seap/tenders', tender.id]">
          <div><h2>{{ tender.title }}</h2>
            <p>{{ tender.buyerName }}{{ tender.county ? ' · ' + tender.county : '' }}{{ tender.category?.name ? ' · ' + tender.category?.name : '' }}</p>
            <p>{{ tender.cpvNames[0] || tender.cpvCodes.join(', ') || 'fără CPV' }}{{ tender.estimatedValue !== null ? ' · ' + (tender.estimatedValue | number:'1.0-0':'ro') + ' ' + (tender.currency ?? 'RON') : '' }}</p>
            <p>Termen: {{ tender.deadline | date:'dd.MM.yyyy, HH:mm' }}</p></div>
          <span class="pill" [class.ready]="tender.status === 'ReadyForBidding'" [class.done]="tender.status === 'BidGenerated'">{{ statusLabel(tender.status) }}</span>
        </a>
      }
    }
  </section>`
})
export class SeapTendersComponent {
  private readonly service = inject(SeapService);
  private readonly profiles = inject(ProfileService);
  private readonly auth = inject(AuthService);
  readonly features = inject(FeaturesService);
  private readonly vault = inject(CompanyVaultService);

  readonly tenders = signal<SeapTender[]>([]);
  readonly loading = signal(true);
  readonly error = signal(false);
  readonly profileLoaded = signal(false);
  readonly isAdmin = computed(() => this.auth.isAdministrator());
  readonly companyId = signal<string | null>(null);
  readonly companyName = signal<string | null>(null);
  readonly county = signal<string | null>(null);
  readonly radiusKm = signal(0);
  readonly radiusActive = signal(false);
  readonly cpvPrefixes = signal<string[]>([]);
  readonly counties = signal<string[]>([]);
  readonly cpvInput = signal('');
  readonly countyDraft = signal('');
  readonly advancedSaving = signal(false);
  readonly advancedMessage = signal('');
  readonly advancedError = signal(false);
  readonly normalizeCpvPrefix = normalizeCpvPrefix;
  readonly countiesList = ROMANIAN_COUNTIES;
  readonly availableCounties = computed(() => this.countiesList.filter(county => !this.counties().includes(county)));
  readonly activeFilterSummary = computed<string | null>(() => {
    const parts: string[] = [];
    if (this.counties().length) parts.push(`județe: ${this.counties().join(', ')}`);
    if (this.cpvPrefixes().length) parts.push(`CPV: ${this.cpvPrefixes().join(', ')}`);
    if (this.radiusActive()) parts.push(`rază: ${this.radiusKm()} km`);
    return parts.length ? parts.join('; ') : null;
  });
  readonly specFile = signal<File | null>(null);
  readonly specBusy = signal(false);
  readonly specMessage = signal('');
  readonly specError = signal(false);
  readonly acceptsSpecResponsibility = signal(false);
  readonly specResult = signal<SpecificationBidResult | null>(null);
  readonly specStage = signal<string>('Se trimite fișierul…');
  private progressRequestId: string | null = null;
  private readonly destroyRef = inject(DestroyRef);

  readonly specStageLabel = computed(() => this.specStage());

  constructor() {
    this.loadProfile();
    // Reactive feed: any filter change (CPV/county chips, radius toggle, saved profile) re-fetches the
    // tenders with the current query params. profileLoaded gates the initial fetch so the saved SEAP
    // Tender Match filters (CPV prefixes + counties) apply from the very first request.
    effect(() => {
      if (!this.profileLoaded()) return;
      this.load();
    });
  }

  private loadProfile(): void {
    this.profiles.getMyCompanies().subscribe({
      next: companies => {
        const company = companies[0];
        if (!company) { this.profileLoaded.set(true); return; }
        this.companyName.set(company.name);
        this.companyId.set(company.id);
        this.profiles.getSupplier(company.id).subscribe({
          next: profile => {
            this.county.set(profile.county || null);
            this.radiusKm.set(profile.marketRadiusKm);
            this.cpvPrefixes.set(profile.seapCpvPrefixes ?? []);
            this.counties.set(profile.seapCounties ?? []);
            this.profileLoaded.set(true);
          },
          error: () => { this.profileLoaded.set(true); }
        });
      },
      error: () => { this.profileLoaded.set(true); }
    });
  }

  toggleRadius(): void {
    this.radiusActive.set(!this.radiusActive());
  }

  load(): void {
    this.loading.set(true);
    this.error.set(false);
    this.service.listTenders({
      radiusKm: this.radiusActive() ? this.radiusKm() : null,
      county: this.county(),
      cpvPrefixes: this.cpvPrefixes(),
      counties: this.counties()
    }).subscribe({
      next: value => { this.tenders.set(value); this.loading.set(false); },
      error: () => { this.loading.set(false); this.error.set(true); }
    });
  }

  addCpvPrefix(): void {
    const value = this.normalizeCpvPrefix(this.cpvInput());
    if (!value) return;
    this.cpvPrefixes.update(prefixes => prefixes.includes(value) ? prefixes : [...prefixes, value]);
    this.cpvInput.set('');
    this.advancedMessage.set(`Prefixul CPV ${value} a fost adăugat la filtrele active.`);
    this.advancedError.set(false);
  }

  removeCpvPrefix(prefix: string): void {
    this.cpvPrefixes.update(prefixes => prefixes.filter(item => item !== prefix));
  }

  addCounty(): void {
    const value = normalizeCounty(this.countyDraft());
    if (!value) return;
    this.counties.update(counties => counties.includes(value) ? counties : [...counties, value]);
    this.countyDraft.set('');
    this.advancedMessage.set(`Județul „${value}” a fost adăugat la filtrele active.`);
    this.advancedError.set(false);
  }

  removeCounty(county: string): void {
    this.counties.update(counties => counties.filter(item => item !== county));
  }

  saveAdvancedFilters(): void {
    const companyId = this.companyId();
    if (!companyId || this.advancedSaving()) return;
    this.advancedSaving.set(true);
    this.advancedMessage.set('');
    this.advancedError.set(false);
    this.service.saveMatchProfile(companyId, this.cpvPrefixes(), this.counties()).subscribe({
      next: saved => {
        this.cpvPrefixes.set(saved.cpvPrefixes);
        this.counties.set(saved.counties);
        this.advancedSaving.set(false);
        this.advancedMessage.set('Filtrele avansate au fost salvate pe profilul firmei.');
      },
      error: () => {
        this.advancedSaving.set(false);
        this.advancedError.set(true);
        this.advancedMessage.set('Filtrele nu au putut fi salvate.');
      }
    });
  }

  onSpecFileSelected(file: File | undefined): void {
    this.specFile.set(file && file.size > 0 ? file : null);
    this.specMessage.set('');
  }

  uploadSpecification(): void {
    const companyId = this.companyId();
    const file = this.specFile();
    if (!companyId || !file || this.specBusy()) return;
    this.specBusy.set(true);
    this.specMessage.set('');
    this.specError.set(false);
    this.specStage.set('Etapa 1/3 — se trimite fișierul și se extrag tabelele din Caietul de Sarcini…');
    this.progressRequestId = crypto.randomUUID();
    this.startProgressPolling(companyId);
    this.vault.uploadSpecification(companyId, file, this.progressRequestId).subscribe({
      next: result => {
        this.stopProgressPolling();
        this.specBusy.set(false);
        this.specFile.set(null);
        this.specResult.set(result);
        this.specError.set(false);
        this.specMessage.set('Pipeline-ul a rulat cu succes' + (result.shadowMode ? ' (Shadow Mode — fără plată).' : '.'));
      },
      error: error => {
        this.stopProgressPolling();
        this.specBusy.set(false);
        this.specError.set(true);
        this.specMessage.set(this.pipelineErrorMessage(error));
      }
    });
  }

  regenerateSpecification(companyId: string): void {
    const result = this.specResult();
    if (!result || this.specBusy()) return;
    this.specBusy.set(true);
    this.specMessage.set('');
    this.specError.set(false);
    this.specStage.set('Etapa 1/3 — se pregătește regenerarea…');
    this.progressRequestId = crypto.randomUUID();
    this.startProgressPolling(companyId);
    this.vault.generateFromSpecification(companyId, result.documentId, this.progressRequestId).subscribe({
      next: updated => {
        this.stopProgressPolling();
        this.specBusy.set(false);
        this.specResult.set(updated);
        this.specError.set(false);
        this.specMessage.set('Propunerea a fost regenerată.');
      },
      error: error => {
        this.stopProgressPolling();
        this.specBusy.set(false);
        this.specError.set(true);
        this.specMessage.set(this.pipelineErrorMessage(error));
      }
    });
  }

  /** Polls the real per-stage pipeline progress while the upload/generate request is in flight. */
  private startProgressPolling(companyId: string): void {
    const requestId = this.progressRequestId;
    if (!requestId) return;
    timer(0, 2000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.vault.getSpecificationProgress(companyId, requestId).subscribe({
          next: progress => {
            const label = PIPELINE_STAGE_LABELS[progress.stage];
            if (label) this.specStage.set(label);
            if (progress.stage === 'Failed') {
              this.specStage.set(progress.message || 'Pipeline-ul a eșuat.');
            }
          },
          // Polling a not-yet-registered key is expected at t=0; keep waiting until the request settles.
          error: () => undefined
        });
      });
  }

  private stopProgressPolling(): void {
    this.progressRequestId = null;
  }

  /** Turns an HTTP/API failure into a friendly, specific message (backend ProblemDetails.detail wins). */
  private pipelineErrorMessage(error: unknown): string {
    const problem = (error as { error?: { detail?: string; errors?: { document?: string[] } } })?.error;
    const detail = problem?.detail ?? problem?.errors?.document?.[0];
    if (detail) return detail;
    return 'Pipeline-ul nu a putut rula. Verifică dacă serviciile Azure Document Intelligence și Claude sunt configurate în acest mediu, apoi încearcă din nou.';
  }

  /** Downloads a generated proposal file (technical .docx or bill of quantities .xlsx) with the auth header. */
  downloadSpecFile(kind: 'TechnicalProposal' | 'BillOfQuantities'): void {
    const companyId = this.companyId();
    const result = this.specResult();
    if (!companyId || !result || this.specBusy()) return;
    this.vault.downloadGeneratedFile(companyId, result.documentId, kind).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = kind === 'TechnicalProposal' ? 'OfertaTehnica.docx' : 'Deviz.xlsx';
        anchor.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.specMessage.set('Fișierul nu a putut fi descărcat. Regenerează propunerea și încearcă din nou.')
    });
  }

  statusLabel(status: SeapTender['status']): string {
    return ({ Imported: 'Importat', ReadyForBidding: 'Gata de ofertare', BidGenerated: 'Ofertă generată', Failed: 'Eroare' })[status];
  }
}
