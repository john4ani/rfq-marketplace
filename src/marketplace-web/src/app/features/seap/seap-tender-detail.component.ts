import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { interval, Subscription, switchMap, timer } from 'rxjs';
import { CompanyQualification, CompanyQualificationService } from '../../core/companies/company-qualification.service';
import { PipelineProgress } from '../../core/companies/company-vault.service';
import { VaultReadinessService } from '../../core/companies/vault-readiness.service';
import { ProfileService } from '../../core/profiles/profile.service';
import { AutoBidResponse, BidStatus, SeapService, SeapTender, TenderPricing } from '../../core/seap/seap.service';

/** Friendly Romanian labels for the real per-stage auto-bid progress reported by the API. */
const AUTO_BID_STAGE_LABELS: Record<PipelineProgress['stage'], string> = {
  Extracting: 'Analizăm anunțul…',
  Generating: 'Claude redactează propunerea tehnică…',
  Finalizing: 'Generăm documentele .docx și .xlsx…',
  Completed: 'Propunerea este gata.',
  Failed: 'Generarea a eșuat.'
};

type TenderTab = 'summary' | 'details' | 'qualification' | 'documents' | 'history';

const VALID_TABS: readonly TenderTab[] = ['summary', 'details', 'qualification', 'documents', 'history'];

@Component({
  selector: 'app-seap-tender-detail',
  imports: [RouterLink, DatePipe, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './seap-tender-detail.component.scss',
  template: `<section class="page">
    <a class="back-link" routerLink="/seap/tenders">← Anunțuri SEAP</a>
    @if (autoBidToastVisible()) {
      <div class="toast" role="status" aria-live="polite">
        <span>AI-ul redactează propunerea tehnică în background. Poți continua să explorezi anunțul.</span>
        <button type="button" class="toast-close" (click)="autoBidToastVisible.set(false)" aria-label="Închide notificarea">×</button>
      </div>
    }
    @if (loading()) { <section class="state" role="status">Se încarcă anunțul…</section> }
    @else if (tender(); as tender) {
      <header><p class="eyebrow">Anunț SEAP · {{ tender.seapExternalId }}</p><h1>{{ tender.title }}</h1>
        <p>{{ tender.buyerName }}{{ tender.county ? ' · ' + tender.county : '' }}{{ tender.category?.name ? ' · ' + tender.category?.name : '' }} · termen {{ tender.deadline | date:'dd.MM.yyyy, HH:mm' }}</p></header>
      <section class="card"><div class="facts">
        <div><span>Valoare estimată</span><strong>{{ tender.estimatedValue !== null ? (tender.estimatedValue | number:'1.0-0':'ro') + ' ' + (tender.currency ?? 'RON') : 'Nespecificată' }}</strong></div>
        <div><span>Coduri CPV</span><strong>{{ tender.cpvCodes.join(', ') || '—' }}</strong></div>
        <div><span>Categorie</span><strong>{{ tender.category?.name || tender.cpvNames[0] || '—' }}</strong></div>
        <div><span>Publicat</span><strong>{{ tender.publicationDate | date:'dd.MM.yyyy' }}</strong></div>
        <div><span>Stare procedură</span><strong>{{ procedureState() }}</strong></div>
      </div></section>

      <nav class="tabs" role="tablist" aria-label="Detalii anunț SEAP">
        <button type="button" class="tab" role="tab" [id]="'tab-summary'" [attr.aria-selected]="activeTab() === 'summary'" [class.active]="activeTab() === 'summary'" aria-controls="panel-summary" (click)="selectTab('summary')">Sinteză &amp; Ofertare AI</button>
        <button type="button" class="tab" role="tab" [id]="'tab-details'" [attr.aria-selected]="activeTab() === 'details'" [class.active]="activeTab() === 'details'" aria-controls="panel-details" (click)="selectTab('details')">Detalii Achiziție</button>
        <button type="button" class="tab" role="tab" [id]="'tab-qualification'" [attr.aria-selected]="activeTab() === 'qualification'" [class.active]="activeTab() === 'qualification'" aria-controls="panel-qualification" (click)="selectTab('qualification')">Condiții de Calificare</button>
        <button type="button" class="tab" role="tab" [id]="'tab-documents'" [attr.aria-selected]="activeTab() === 'documents'" [class.active]="activeTab() === 'documents'" aria-controls="panel-documents" (click)="selectTab('documents')">Documente Originale</button>
        @if (bidStatus()?.status === 'BidGenerated') {
          <button type="button" class="tab" role="tab" [id]="'tab-history'" [attr.aria-selected]="activeTab() === 'history'" [class.active]="activeTab() === 'history'" aria-controls="panel-history" (click)="selectTab('history')">Istoric Ofertă</button>
        }
      </nav>

      @switch (activeTab()) {
        @case ('summary') {
          <section class="card" id="panel-summary" role="tabpanel" aria-labelledby="tab-summary">
            <div class="card-head"><h2>Sinteză AI</h2></div>
            @switch (summaryState()) {
              @case ('loading') {
                <div class="skeleton" role="status" aria-busy="true" aria-label="Sinteza AI se generează">
                  <span></span><span></span><span></span>
                </div>
              }
              @case ('ready') {
                <ul class="bullets">@for (point of summaryPoints(); track point) { <li>{{ point }}</li> }</ul>
              }
              @default {
                <div class="state-box" [class.error]="summaryState() === 'error'">
                  <p class="empty">@if (summaryState() === 'error') { Sinteza AI nu a putut fi generată în acest moment. } @else { Sinteza AI nu este generată pentru acest anunț. }</p>
                  <button type="button" (click)="generateSummary()">{{ summaryState() === 'error' ? 'Încearcă din nou' : 'Generează sinteza AI' }}</button>
                </div>
              }
            }
          </section>
          <section class="card">
            @if (bidStatus(); as status) {
          @if (status.status === 'BidGenerated') {
            <div class="note info">
              <strong>Oferta automată este gata.</strong>
              <p class="hint">Documentele generate (propunerea tehnică și lista de cantități) sunt în tab-ul Istoric Ofertă.</p>
              <button type="button" (click)="selectTab('history')">Deschide Istoricul</button>
            </div>
          }
          @else {
            @if (companyId(); as companyId) {
              @if (readinessWarning()) {
                <div class="note warn readiness-warning" role="alert">
                  <strong>Atenție:</strong> Profilul firmei / Vault-ul este incomplet. Oferta va folosi placeholdere.
                  <a class="readiness-link" [routerLink]="['/account/company', companyId]">Completează aici</a>
                </div>
              }
            }
            <div class="actions">
              <button type="button" [disabled]="busy()" (click)="startAutoBid(tender.id)">
                @if (busy()) { <span class="spinner" aria-hidden="true"></span> Se redactează propunerea… }
                @else { {{ pricing().shadowMode ? 'AI Auto-Bid · gratuit' : pricing().freeGenerationsRemaining > 0 ? 'AI Auto-Bid · gratuit' : 'AI Auto-Bid · ' + pricing().priceEur + ' €' }} }
              </button>
              @if (busy()) {
                <div class="auto-bid-progress" role="status" aria-live="polite">
                  <div class="progress-track"><div class="progress-fill" [style.width.%]="autoBidPercent()"></div></div>
                  <p class="progress-stage">{{ autoBidStage() || 'Analizăm anunțul și pregătim propunerea tehnică…' }}</p>
                  <small class="progress-hint">AI-ul redactează propunerea în background. De obicei durează sub un minut.</small>
                </div>
              }
              @if (pricing().shadowMode) { <span class="note info">Mod de test — generarea este gratuită în această etapă. După lansare, prețul estimat pentru acest anunț: {{ pricing().priceEur }} €.</span> }
              @else if (pricing().freeGenerationsRemaining > 0) { <span class="note info">Prima generare este gratuită. Următoarele: {{ pricing().priceEur }} € (valoare estimată).</span> }
              @else { <span class="note info">Generare {{ pricing().priceEur }} €, pe baza valorii estimate a achiziției.</span> }
              <span class="note warn">Generăm propunerea tehnică și lista de cantități pe baza datelor din Depozitul de Documente.
                <span class="copilot">CompaSpot este un instrument software de redactare asistată, nu un furnizor de consultanță juridică sau tehnică. Verifică și asumă-ți conținutul înainte de semnarea electronică și depunerea în SEAP.</span></span>
            </div>
          }
          @if (message()) { <p class="note" [class.error]="messageError()" role="status">{{ message() }}</p> }
        }
      </section>
        }
        @case ('details') {
          <section class="card" id="panel-details" role="tabpanel" aria-labelledby="tab-details">
            <h2>Detalii Achiziție</h2>
            @if (tender.description) { <p class="description">{{ tender.description }}</p> }
            @else { <p class="empty">{{ descriptionPlaceholder() }}</p> }
            <dl class="details">
              <div><dt>Criteriul de atribuire</dt><dd>{{ tender.awardCriterion || unavailableLabel() }}</dd></div>
              <div><dt>Tipul procedurii</dt><dd>{{ procedureType() }}</dd></div>
              <div><dt>Sursa de finanțare</dt><dd>{{ tender.fundingSource || unavailableLabel() }}</dd></div>
              <div><dt>Durata contractului</dt><dd>{{ tender.contractDuration || unavailableLabel() }}</dd></div>
            </dl>
            <p class="hint">
              @if (hasAiSummary()) { „Date indisponibile în anunț" înseamnă că autoritatea contractantă nu a publicat informația în SEAP. }
              @else { Aceste detalii sunt extrase automat din anunț. <button type="button" class="hint-action" (click)="openSummaryAndGenerate()">Generează sinteza AI</button> }
            </p>
          </section>
        }
        @case ('qualification') {
          <section class="card" id="panel-qualification" role="tabpanel" aria-labelledby="tab-qualification">
            <h2>Condiții de Calificare</h2>
            <dl class="details">
              <div><dt>Cuantumul garanției de participare</dt><dd>{{ tender.participationGuarantee || unavailableLabel() }}</dd></div>
              <div><dt>Capacitate tehnică / profesională</dt><dd>{{ listOrPlaceholder(tender.qualificationRequirements) }}</dd></div>
              <div><dt>Personal solicitat</dt><dd>{{ listOrPlaceholder(tender.keyPersonnel) }}</dd></div>
              <div><dt>Certificate și standarde (ISO)</dt><dd>{{ listOrPlaceholder(tender.certificates) }}</dd></div>
            </dl>
            <p class="hint">
              @if (hasAiSummary()) { „Date indisponibile în anunț" înseamnă că autoritatea contractantă nu a publicat informația în SEAP. }
              @else { Aceste detalii sunt extrase automat din anunț. <button type="button" class="hint-action" (click)="openSummaryAndGenerate()">Generează sinteza AI</button> }
            </p>
          </section>
        }
        @case ('documents') {
          <section class="card" id="panel-documents" role="tabpanel" aria-labelledby="tab-documents">
            <div class="card-head"><h2>Documente Originale</h2></div>
            @if (documentsRefreshing()) {
              <div class="skeleton" role="status" aria-busy="true" aria-label="Se caută documentele anunțului">
                <span></span><span></span><span></span>
              </div>
            } @else if (tender.attachments.length) {
              <ul class="doc-list">
                @for (attachment of tender.attachments; track attachment.id) {
                  <li>
                    <span class="doc-name" [title]="attachment.fileName">{{ attachment.fileName }}</span>
                    <button type="button" [disabled]="downloadingAttachmentId() !== null" (click)="downloadAttachment(attachment.id)">{{ downloadingAttachmentId() === attachment.id ? 'Se descarcă…' : 'Descarcă' }}</button>
                  </li>
                }
              </ul>
            } @else {
              <div class="state-box" [class.error]="documentsError()">
                <p class="empty">@if (documentsError()) { Documentele anunțului nu au putut fi încărcate în acest moment. } @else { Nu au fost găsite documente publicate pentru acest anunț în SEAP. }</p>
                @if (documentsError()) { <button type="button" (click)="refreshDocuments()">Reîncearcă</button> }
              </div>
            }
          </section>
        }
        @case ('history') {
          <section class="card" id="panel-history" role="tabpanel" aria-labelledby="tab-history">
            <div class="card-head"><h2>Istoric Ofertă</h2>
              @if (bidStatus()?.status === 'BidGenerated') { <span class="pill done">Ofertă generată</span> }
            </div>
            @if (bidStatus(); as status) {
              @if (status.status === 'BidGenerated') {
                @if (status.proposalTitle) { <p class="proposal-title">{{ status.proposalTitle }}</p> }
                <div class="downloads">
                  <button type="button" [disabled]="!acceptsResponsibility() || downloading()" (click)="downloadBidFile('TechnicalProposal')">Descarcă propunerea tehnică (.docx)</button>
                  <button type="button" [disabled]="!acceptsResponsibility() || downloading()" (click)="downloadBidFile('BillOfQuantities')">Descarcă lista de cantități (.xlsx)</button>
                </div>
                <label class="acceptance"><input type="checkbox" [checked]="acceptsResponsibility()" (change)="acceptsResponsibility.set($any($event.target).checked)" />
                  <span>Îmi asum responsabilitatea verificării și asumării conținutului acestor documente înainte de semnarea electronică și depunerea în SEAP.</span></label>
                <p class="hint">Documentele au fost generate de CompaSpot pe baza datelor din Depozitul de Documente. Verifică-le și asumă-ți conținutul înainte de depunerea în SEAP.</p>
              } @else {
                <p class="empty">Nu există încă o ofertă generată pentru acest anunț.</p>
                <button type="button" (click)="selectTab('summary')">Generează oferta</button>
              }
            }
          </section>
        }
      }
    }
  </section>`
})

export class SeapTenderDetailComponent {
  private readonly service = inject(SeapService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);
  private readonly profiles = inject(ProfileService);
  private readonly readiness = inject(VaultReadinessService);
  private readonly qualifications = inject(CompanyQualificationService);
  private readonly tenderId = this.route.snapshot.paramMap.get('id') ?? '';
  readonly tender = signal<SeapTender | null>(null);
  readonly bidStatus = signal<BidStatus | null>(null);
  readonly pricing = signal<TenderPricing>({ estimatedValueEur: 0, priceEur: 0, freeGenerationsRemaining: 0, shadowMode: true });
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly message = signal('');
  readonly messageError = signal(false);
  readonly acceptsResponsibility = signal(false);
  readonly downloading = signal(false);
  readonly autoBidStage = signal('');
  /** Simulated deterministic progress while the auto-bid request is in flight (0 → 90%, then waits for the
   *  real completion). Never reaches 100% on its own — only the Completed stage does. */
  readonly autoBidPercent = signal(0);
  /** Non-blocking toast confirming the AI is drafting the technical proposal in the background. */
  readonly autoBidToastVisible = signal(false);
  /** Interval driving the simulated progress; stopped when the request settles. */
  private autoBidProgressTimer: Subscription | null = null;
  /** The user's represented company — the vault that feeds the RAG context for this tender. */
  readonly companyId = signal<string | null>(null);
  /** Vault/qualification readiness: null while unknown (the API responses are still in flight); a failed
   *  readiness API never blocks the generation — it degrades to "assume ready" so the warning is honest. */
  readonly vaultReady = signal<boolean | null>(null);
  readonly qualificationReady = signal<boolean | null>(null);
  /** Contextual warning shown above the generate button when the vault or the qualification profile is
   *  incomplete — the offer would fall back to placeholders. Only shows once both checks are known. */
  readonly readinessWarning = computed(() => {
    const vault = this.vaultReady();
    const qualification = this.qualificationReady();
    if (vault === null || qualification === null) return false;
    return !vault || !qualification;
  });
  /** The active tab; initialised from the shareable ?tab= query parameter. */
  readonly activeTab = signal<TenderTab>(this.initialTab());
  /** Attachment currently being downloaded (prevents double-clicks); null when idle. */
  readonly downloadingAttachmentId = signal<string | null>(null);

  /** Real AI-synthesis bullets returned by the backend; empty while the notice has no summary. */
  readonly summaryPoints = computed(() => this.tender()?.summaryPoints ?? []);
  /** True once the AI extraction exists (bullets present). Only then does a null detail field genuinely
   *  mean the notice does not state it — before that the value is simply "not extracted yet" (the on-demand
   *  search defers the summarizer, ADR-011), so the placeholder must not claim it is unavailable. */
  readonly hasAiSummary = computed(() => (this.tender()?.summaryPoints?.length ?? 0) > 0);
  /** On-demand generation state: 'idle' (not generated yet, not requested), 'generating' (request in
   *  flight), 'error' (last attempt failed — the user retries deliberately, never an endless skeleton). */
  readonly summaryGeneration = signal<'idle' | 'generating' | 'error'>('idle');
  /** The „Sinteză AI" card state: 'ready' bullets, 'loading' skeleton only while a real request runs,
   *  'empty' actionable empty state, 'error' recoverable failure. */
  readonly summaryState = computed<'ready' | 'loading' | 'empty' | 'error'>(() => {
    if ((this.tender()?.summaryPoints ?? []).length > 0) return 'ready';
    const generation = this.summaryGeneration();
    if (generation === 'generating') return 'loading';
    return generation === 'error' ? 'error' : 'empty';
  });

  /** Requests the AI summary on demand (idempotent server-side). A deliberate user action — the page never
   *  auto-fires an LLM call or pretends to process when nothing is running. */
  generateSummary(): void {
    const tender = this.tender();
    if (!tender || this.summaryGeneration() === 'generating') return;
    this.summaryGeneration.set('generating');
    this.service.generateSummary(tender.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: updated => {
        this.tender.set(updated);
        this.summaryGeneration.set('idle');
      },
      error: () => this.summaryGeneration.set('error')
    });
  }

  /** Placeholder for the full procedure description: after the extraction it means the notice does not
   *  publish it; before the extraction (search imports defer the summarizer) it is simply not available in
   *  the search result — the on-demand summary can still produce a readable acquisition summary. */
  descriptionPlaceholder(): string {
    return this.hasAiSummary()
      ? 'Descrierea completă a procedurii nu este publicată în SEAP.'
      : 'Descrierea completă a procedurii nu este disponibilă în rezultatul căutării. Poți genera sinteza AI pentru un rezumat.';
  }

  /** Placeholder for a detail field: after the AI extraction ran, a null value means the notice genuinely
   *  does not state it; before the extraction exists (search-imported tenders defer the summarizer) the
   *  value is simply not extracted yet — never invent data, never claim absence prematurely. */
  unavailableLabel(): string {
    return this.hasAiSummary() ? 'Date indisponibile în anunț' : 'Necesită generarea sintezei AI';
  }

  /** True while the published documents are being resolved on demand (a cheap, idempotent request). */
  readonly documentsRefreshing = signal(false);
  /** True when the last on-demand document resolution failed — the user retries deliberately. */
  readonly documentsError = signal(false);
  /** The on-demand refresh runs once per page view (only when the tab is opened and no documents exist). */
  private documentsRefreshAttempted = false;

  /** Resolves the published documents on demand (idempotent server-side). Never fires automatically more
   *  than once per view; failures surface a recoverable state with a deliberate retry. */
  refreshDocuments(): void {
    const tender = this.tender();
    if (!tender || this.documentsRefreshing()) return;
    this.documentsRefreshing.set(true);
    this.documentsError.set(false);
    this.service.refreshDocuments(tender.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: updated => {
        this.tender.set(updated);
        this.documentsRefreshing.set(false);
      },
      error: () => {
        this.documentsRefreshing.set(false);
        this.documentsError.set(true);
      }
    });
  }

  private openDocumentsTab(): void {
    if (this.documentsRefreshAttempted) return;
    this.documentsRefreshAttempted = true;
    const tender = this.tender();
    if (!tender || tender.attachments.length > 0) return;
    this.refreshDocuments();
  }
  /** „Stare procedură" card: backend-derived value, else derived from the deadline. */
  readonly procedureState = computed(() => {
    const tender = this.tender();
    if (tender?.procedureState) return tender.procedureState;
    return tender && new Date(tender.deadline).getTime() >= Date.now() ? 'În desfășurare' : 'Încheiată';
  });
  /** „Tipul procedurii": backend-derived value, else derived from the SCN/CN external-id prefix. */
  readonly procedureType = computed(() => {
    const tender = this.tender();
    if (tender?.procedureType) return tender.procedureType;
    return tender?.seapExternalId.toUpperCase().startsWith('SCN') ? 'Procedură simplificată' : 'Licitare deschisă';
  });

  constructor() {
    this.profiles.getMyCompanies().subscribe({
      next: companies => {
        const company = companies[0];
        this.companyId.set(company?.id ?? null);
        if (company) this.loadReadiness(company.id);
      },
      error: () => undefined
    });
    this.service.getTender(this.tenderId).subscribe({
      next: value => { this.tender.set(value); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
    this.service.getBidStatus(this.tenderId).subscribe({
      next: value => {
        this.bidStatus.set(value);
        // A shareable ?tab=history link on a tender without a generated offer falls back to the summary tab.
        if (value.status !== 'BidGenerated' && this.activeTab() === 'history') this.activeTab.set('summary');
      }
    });
    this.service.getTenderPricing(this.tenderId).subscribe({ next: value => this.pricing.set(value) });
    // When the documents tab is open (directly or via ?tab=documents) and no document is attached yet,
    // resolve the published documents on demand (once per view).
    effect(() => {
      if (this.activeTab() === 'documents' && this.tender() !== null) this.openDocumentsTab();
    });
  }

  /** Reads the active tab from the shareable ?tab= query parameter; defaults to „Sinteză & Ofertare AI". */
  private initialTab(): TenderTab {
    const tab = this.route.snapshot.queryParamMap.get('tab') as TenderTab | null;
    return VALID_TABS.includes(tab ?? 'summary') ? (tab ?? 'summary') : 'summary';
  }

  /** Switches the tab and keeps the URL shareable (?tab=…). Opening the documents tab when no document is
   *  attached triggers the on-demand resolution (once per view). */
  selectTab(tab: TenderTab): void {
    if (this.activeTab() === tab) return;
    this.activeTab.set(tab);
    if (tab === 'documents') this.openDocumentsTab();
    void this.router.navigate([], { queryParams: { tab }, queryParamsHandling: 'merge', replaceUrl: true })
      .catch(() => undefined);
  }

  /** Renders a list of qualification details or a contextual placeholder (absent vs not-yet-extracted). */
  listOrPlaceholder(items: string[] | null | undefined): string {
    return items && items.length > 0 ? items.join(' · ') : this.unavailableLabel();
  }

  /** Takes the user to the „Sinteză & Ofertare AI" tab and starts the on-demand extraction (a deliberate
   *  user action — the page never auto-fires an LLM call on its own). */
  openSummaryAndGenerate(): void {
    this.selectTab('summary');
    this.generateSummary();
  }

  /** Downloads one original authority file through the authenticated API (browsers cannot open local://s3://
   *  references directly). Mirrors the generated-bid download flow: blob → temporary anchor. */
  downloadAttachment(attachmentId: string): void {
    if (this.downloadingAttachmentId() !== null) return;
    const tender = this.tender();
    if (!tender) return;
    this.downloadingAttachmentId.set(attachmentId);
    this.service.downloadAttachment(tender.id, attachmentId).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = tender.attachments.find(item => item.id === attachmentId)?.fileName ?? 'document.pdf';
        anchor.click();
        URL.revokeObjectURL(url);
        this.downloadingAttachmentId.set(null);
      },
      error: () => this.downloadingAttachmentId.set(null)
    });
  }

  startAutoBid(tenderId: string): void {
    if (this.busy()) return;
    this.runAutoBid(tenderId);
  }

  private runAutoBid(tenderId: string): void {
    this.busy.set(true);
    this.message.set('');
    this.messageError.set(false);
    this.autoBidStage.set('');
    // The button stays visible (disabled + spinner) and the interface remains usable: a simulated
    // deterministic progress bar climbs to 90% while the real per-stage progress is polled, and a
    // non-blocking toast confirms the AI is drafting the proposal in the background.
    this.autoBidPercent.set(0);
    this.autoBidToastVisible.set(true);
    this.autoBidProgressTimer = interval(600).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.autoBidPercent.update(percent => (percent < 90 ? Math.min(90, percent + 1 + Math.floor(Math.random() * 2)) : percent));
    });
    // The API reports real per-stage progress under this id while the request is in flight; poll it
    // so the user sees what is actually happening instead of an endless "generating…" line.
    const progressRequestId = crypto.randomUUID();
    this.startProgressPolling(progressRequestId);
    this.service.startAutoBid(tenderId, this.companyId(), progressRequestId).subscribe({
      next: response => this.handleAutoBid(response),
      error: (error: unknown) => {
        this.busy.set(false);
        this.autoBidToastVisible.set(false);
        this.autoBidPercent.set(0);
        this.stopAutoBidProgressTimer();
        this.messageError.set(true);
        this.message.set(this.autoBidErrorMessage(error));
        // A failed generation never consumes the free entitlement; keep the price preview accurate.
        this.refreshPricing();
        this.stopProgressPolling();
      }
    });
  }

  /** Turns an HTTP/API failure into a friendly, specific message (backend ProblemDetails.detail wins,
   *  mirroring the Caiet-de-sarcini pipeline error handling). */
  private autoBidErrorMessage(error: unknown): string {
    const detail = (error as { error?: { detail?: string } })?.error?.detail;
    if (detail) return detail;
    return 'Ofertarea automată nu a putut fi pornită. Încearcă din nou.';
  }

  private handleAutoBid(response: AutoBidResponse): void {
    this.stopProgressPolling();
    this.stopAutoBidProgressTimer();
    if (response.ready) {
      this.busy.set(false);
      this.autoBidToastVisible.set(false);
      this.autoBidPercent.set(100);
      this.message.set(response.freeGeneration ? 'Prima generare a fost gratuită. Oferta este gata.' : 'Oferta automată este gata.');
      this.refreshStatus();
      // The free entitlement (if any) was consumed server-side; refresh the price preview so the
      // "Prima generare este gratuită" note/button reflect the remaining count immediately.
      this.refreshPricing();
      return;
    }
    if (response.checkoutUrl) {
      window.location.href = response.checkoutUrl;
      this.busy.set(false);
      this.autoBidToastVisible.set(false);
      this.autoBidPercent.set(0);
      this.pollUntilReady();
      return;
    }
    this.busy.set(false);
    this.autoBidToastVisible.set(false);
    this.autoBidPercent.set(0);
    this.messageError.set(true);
    this.message.set('Plata nu este configurată. Contactează suportul.');
  }

  /** Downloads one auto-generated proposal file through the API (the stored local://s3:// reference is
   *  resolved server-side; browsers cannot navigate those schemes directly). The downloaded file name
   *  carries the SEAP notice id (e.g. „PropunereTehnica_SCN1179243.docx") so the file is recognizable
   *  when signed and submitted in SEAP. */
  downloadBidFile(kind: 'TechnicalProposal' | 'BillOfQuantities'): void {
    if (this.downloading() || !this.acceptsResponsibility()) return;
    this.downloading.set(true);
    this.message.set('');
    this.service.downloadBidFile(this.tenderId, kind).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        const noticeId = this.tender()?.seapExternalId;
        anchor.download = kind === 'TechnicalProposal'
          ? `PropunereTehnica${noticeId ? '_' + noticeId : ''}.docx`
          : `ListaCantitati${noticeId ? '_' + noticeId : ''}.xlsx`;
        anchor.click();
        URL.revokeObjectURL(url);
        this.downloading.set(false);
      },
      error: () => {
        this.downloading.set(false);
        this.messageError.set(true);
        this.message.set('Fișierul nu a putut fi descărcat. Regenerează propunerea și încearcă din nou.');
      }
    });
  }

  private pollUntilReady(): void {
    interval(5000).pipe(
      takeUntilDestroyed(this.destroyRef),
      switchMap(() => this.service.getBidStatus(this.tenderId))
    ).subscribe({
      next: status => {
        this.bidStatus.set(status);
        if (status.status === 'BidGenerated') {
          this.busy.set(false);
          this.message.set('Oferta automată este gata.');
          this.refreshPricing();
        }
      },
      error: () => { /* keep polling; transient */ }
    });
  }

  private refreshStatus(): void {
    this.service.getBidStatus(this.tenderId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: status => {
        this.bidStatus.set(status);
        if (status.status === 'BidGenerated') {
          // The generation finished — take the user to the Istoric tab where the documents live.
          if (this.activeTab() === 'summary') this.selectTab('history');
          this.busy.set(false);
        } else {
          this.busy.set(true);
        }
      },
      error: () => this.busy.set(false)
    });
  }

  /** Reloads the fair-pricing preview (free generations remaining / price) after a generation consumed it. */
  private refreshPricing(): void {
    this.service.getTenderPricing(this.tenderId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: value => this.pricing.set(value),
      error: () => undefined
    });
  }

  private autoBidProgressRequestId: string | null = null;

  /** Polls the real per-stage progress (Generating → Finalizing → Completed/Failed) reported by the API
   *  while the POST /auto-bid request is in flight, mirroring the Caiet-de-sarcini pipeline UX. */
  private startProgressPolling(requestId: string): void {
    this.autoBidProgressRequestId = requestId;
    timer(0, 2000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        if (this.autoBidProgressRequestId !== requestId) return;
        this.service.getAutoBidProgress(this.tenderId, requestId).subscribe({
          next: progress => {
            if (this.autoBidProgressRequestId !== requestId) return;
            this.autoBidStage.set(progress.message ?? AUTO_BID_STAGE_LABELS[progress.stage]);
          },
          // A not-yet-registered key at t=0 (or an older API without the endpoint) is expected; keep polling.
          error: () => undefined
        });
      });
  }

  private stopProgressPolling(): void {
    this.autoBidProgressRequestId = null;
  }

  private stopAutoBidProgressTimer(): void {
    this.autoBidProgressTimer?.unsubscribe();
    this.autoBidProgressTimer = null;
  }

  /** Loads the vault/qualification readiness once for the represented company so the summary tab can show
   *  the contextual „Profilul firmei / Vault-ul este incomplet" warning above the generate button. A failed
   *  readiness API degrades to "ready" — it never blocks the generation. */
  private loadReadiness(companyId: string): void {
    this.readiness.hasUsableDocuments(companyId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: usable => this.vaultReady.set(usable),
      error: () => this.vaultReady.set(true)
    });
    this.qualifications.get(companyId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: qualification => this.qualificationReady.set(this.isQualificationComplete(qualification)),
      error: () => this.qualificationReady.set(true)
    });
  }

  /** The SEAP qualification profile counts as complete when the legal data exists and the company has at
   *  least one proof of capability (similar-experience contract, certificate, or key expert). */
  private isQualificationComplete(qualification: CompanyQualification): boolean {
    return qualification.legal !== null
      && (qualification.experience.length > 0 || qualification.certificates.length > 0 || qualification.personnel.length > 0);
  }
}

