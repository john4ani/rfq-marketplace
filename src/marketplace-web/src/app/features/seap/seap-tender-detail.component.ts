import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { interval, switchMap, timer } from 'rxjs';
import { PipelineProgress } from '../../core/companies/company-vault.service';
import { VaultReadinessService } from '../../core/companies/vault-readiness.service';
import { ProfileService } from '../../core/profiles/profile.service';
import { AutoBidResponse, BidStatus, SeapService, SeapTender, TenderPricing } from '../../core/seap/seap.service';
import { VaultPromptComponent } from '../../shared/vault-prompt.component';

/** Friendly Romanian labels for the real per-stage auto-bid progress reported by the API. */
const AUTO_BID_STAGE_LABELS: Record<PipelineProgress['stage'], string> = {
  Extracting: 'Analizăm anunțul…',
  Generating: 'Claude redactează propunerea tehnică…',
  Finalizing: 'Generăm documentele .docx și .xlsx…',
  Completed: 'Propunerea este gata.',
  Failed: 'Generarea a eșuat.'
};

/**
 * Placeholder AI-synthesis bullets shown while the notice-detail parser does not yet extract a real
 * summary from the SEAP notice. Clearly labelled „Orientativ" so they are never mistaken for a real
 * AI-generated summary.
 */
const MOCK_SUMMARY_POINTS = [
  'Garanția de participare: aproximativ 2% din valoarea estimată a contractului.',
  'Experiență: minim un contract similar, finalizat în ultimii 3 ani (rulaj și referințe).',
  'Criteriu cheie: prețul cel mai scăzut.',
  'Documente de calificare: formularele standard SEAP și certificatele cerute în Fișa de date.'
];

type TenderTab = 'summary' | 'details' | 'qualification' | 'documents';

const VALID_TABS: readonly TenderTab[] = ['summary', 'details', 'qualification', 'documents'];

@Component({
  selector: 'app-seap-tender-detail',
  imports: [RouterLink, DatePipe, DecimalPipe, VaultPromptComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './seap-tender-detail.component.scss',
  template: `<section class="page">
    <a class="back-link" routerLink="/seap/tenders">← Anunțuri SEAP</a>
    @if (vaultPromptVisible() && companyId(); as companyId) {
      <app-vault-prompt [companyId]="companyId" (dismissed)="onVaultPromptDismissed()" />
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
      </nav>

      @switch (activeTab()) {
        @case ('summary') {
          <section class="card" id="panel-summary" role="tabpanel" aria-labelledby="tab-summary">
            <div class="card-head"><h2>Sinteză AI</h2>@if (summaryIsMock()) { <span class="pill orientativ">Orientativ</span> }</div>
            <ul class="bullets">@for (point of summaryPoints(); track point) { <li>{{ point }}</li> }</ul>
            <p class="hint">Sinteza orientativă este generată din informațiile disponibile în anunț. Detaliile complete vor fi extrase automat în curând.</p>
          </section>
          <section class="card">
            @if (bidStatus(); as status) {
          @if (status.status === 'BidGenerated') {
            <div class="note info"><strong>Oferta automată este gata.</strong>
              <div class="downloads">
                <button type="button" [disabled]="downloading()" (click)="downloadBidFile('TechnicalProposal')">Descarcă propunerea tehnică (.docx)</button>
                <button type="button" [disabled]="downloading()" (click)="downloadBidFile('BillOfQuantities')">Descarcă lista de cantități (.xlsx)</button>
              </div>
              <label class="acceptance"><input type="checkbox" [checked]="acceptsResponsibility()" (change)="acceptsResponsibility.set($any($event.target).checked)" />
                <span>Îmi asum responsabilitatea verificării și asumării conținutului acestor documente înainte de semnarea electronică și depunerea în SEAP.</span></label></div>
          } @else if (busy()) {
            <div class="note info" role="status">
              <strong>Redactăm oferta…</strong>
              <div class="progress" aria-hidden="true"><span></span></div>
              <p class="progress-stage">{{ autoBidStage() || 'Analizăm anunțul și pregătim propunerea tehnică…' }}</p>
              <small class="progress-hint">De obicei durează sub un minut. Poți reveni oricând la anunț.</small>
            </div>
          }
          @else {
            <div class="actions">
              <button type="button" [disabled]="busy()" (click)="startAutoBid(tender.id)">{{ pricing().shadowMode ? 'AI Auto-Bid · gratuit' : pricing().freeGenerationsRemaining > 0 ? 'AI Auto-Bid · gratuit' : 'AI Auto-Bid · ' + pricing().priceEur + ' €' }}</button>
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
            @else { <p class="empty">Descrierea completă a procedurii va fi disponibilă după extragerea anunțului.</p> }
            <dl class="details">
              <div><dt>Criteriul de atribuire</dt><dd>{{ tender.awardCriterion || 'Prețul cel mai scăzut' }}</dd></div>
              <div><dt>Tipul procedurii</dt><dd>{{ procedureType() }}</dd></div>
              <div><dt>Sursa de finanțare</dt><dd>{{ tender.fundingSource || '—' }}</dd></div>
              <div><dt>Durata contractului</dt><dd>{{ tender.contractDuration || '—' }}</dd></div>
            </dl>
            <p class="hint">Valorile marcate cu „—" vor fi completate automat după extragerea completă a anunțului.</p>
          </section>
        }
        @case ('qualification') {
          <section class="card" id="panel-qualification" role="tabpanel" aria-labelledby="tab-qualification">
            <h2>Condiții de Calificare</h2>
            <dl class="details">
              <div><dt>Cuantumul garanției de participare</dt><dd>{{ tender.participationGuarantee || '—' }}</dd></div>
              <div><dt>Capacitate tehnică / profesională</dt><dd>{{ listOrPlaceholder(tender.qualificationRequirements) }}</dd></div>
              <div><dt>Personal solicitat</dt><dd>{{ listOrPlaceholder(tender.keyPersonnel) }}</dd></div>
              <div><dt>Certificate și standarde (ISO)</dt><dd>{{ listOrPlaceholder(tender.certificates) }}</dd></div>
            </dl>
            <p class="hint">Condițiile de calificare vor fi extrase automat din documentele anunțului.</p>
          </section>
        }
        @case ('documents') {
          <section class="card" id="panel-documents" role="tabpanel" aria-labelledby="tab-documents">
            <h2>Documente Originale</h2>
            @if (tender.attachments.length) {
              <ul class="doc-list">
                @for (attachment of tender.attachments; track attachment.id) {
                  <li>
                    <span class="doc-name" [title]="attachment.fileName">{{ attachment.fileName }}</span>
                    <button type="button" [disabled]="downloadingAttachmentId() !== null" (click)="downloadAttachment(attachment.id)">{{ downloadingAttachmentId() === attachment.id ? 'Se descarcă…' : 'Descarcă' }}</button>
                  </li>
                }
              </ul>
            } @else {
              <p class="empty">Niciun document atașat încă. Fișa de date și caietul de sarcini vor apărea aici după descărcarea lor.</p>
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
  /** The user's represented company — the vault that feeds the RAG context for this tender. */
  readonly companyId = signal<string | null>(null);
  /** Shows the „no relevant documents yet" recommendation before the first auto-bid. */
  readonly vaultPromptVisible = signal(false);
  /** The active tab; initialised from the shareable ?tab= query parameter. */
  readonly activeTab = signal<TenderTab>(this.initialTab());
  /** Attachment currently being downloaded (prevents double-clicks); null when idle. */
  readonly downloadingAttachmentId = signal<string | null>(null);
  /** The generation that waits for the user's choice after the repository prompt is dismissed. */
  private pendingAutoBid: (() => void) | null = null;

  /** AI-synthesis bullets: real values when the backend provides them, mock bullets otherwise. */
  readonly summaryPoints = computed(() => {
    const points = this.tender()?.summaryPoints;
    return points && points.length > 0 ? points : MOCK_SUMMARY_POINTS;
  });
  readonly summaryIsMock = computed(() => {
    const points = this.tender()?.summaryPoints;
    return !points || points.length === 0;
  });
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
      next: companies => this.companyId.set(companies[0]?.id ?? null),
      error: () => undefined
    });
    this.service.getTender(this.tenderId).subscribe({
      next: value => { this.tender.set(value); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
    this.service.getBidStatus(this.tenderId).subscribe({ next: value => this.bidStatus.set(value) });
    this.service.getTenderPricing(this.tenderId).subscribe({ next: value => this.pricing.set(value) });
  }

  /** Reads the active tab from the shareable ?tab= query parameter; defaults to „Sinteză & Ofertare AI". */
  private initialTab(): TenderTab {
    const tab = this.route.snapshot.queryParamMap.get('tab') as TenderTab | null;
    return VALID_TABS.includes(tab ?? 'summary') ? (tab ?? 'summary') : 'summary';
  }

  /** Switches the tab and keeps the URL shareable (?tab=…). */
  selectTab(tab: TenderTab): void {
    if (this.activeTab() === tab) return;
    this.activeTab.set(tab);
    void this.router.navigate([], { queryParams: { tab }, queryParamsHandling: 'merge', replaceUrl: true })
      .catch(() => undefined);
  }

  /** Renders a list of qualification details or an elegant „—" placeholder when not extracted yet. */
  listOrPlaceholder(items: string[] | null | undefined): string {
    return items && items.length > 0 ? items.join(' · ') : '—';
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
    // Before generating an offer the page verifies the Document Repository has usable documents; when it
    // does not, the shared prompt is shown and the generation waits for the user's choice.
    this.verifyVaultThen(() => this.runAutoBid(tenderId));
  }

  /** Checks the repository before a material action and shows the shared prompt when no usable documents
   *  exist. A vault API failure never blocks the action — the user can proceed regardless. */
  private verifyVaultThen(proceed: () => void): void {
    const companyId = this.companyId();
    if (!companyId || this.readiness.isPromptDismissed(companyId)) { proceed(); return; }
    this.readiness.hasUsableDocuments(companyId).subscribe({
      next: usable => {
        if (usable) { proceed(); }
        else {
          this.pendingAutoBid = proceed;
          this.vaultPromptVisible.set(true);
        }
      },
      error: () => proceed()
    });
  }

  /** Called when the user dismisses the repository prompt — the pending auto-bid then proceeds. */
  onVaultPromptDismissed(): void {
    this.vaultPromptVisible.set(false);
    const proceed = this.pendingAutoBid;
    this.pendingAutoBid = null;
    proceed?.();
  }

  private runAutoBid(tenderId: string): void {
    this.busy.set(true);
    this.message.set('');
    this.messageError.set(false);
    this.autoBidStage.set('');
    // The API reports real per-stage progress under this id while the request is in flight; poll it
    // so the user sees what is actually happening instead of an endless "generating…" line.
    const progressRequestId = crypto.randomUUID();
    this.startProgressPolling(progressRequestId);
    this.service.startAutoBid(tenderId, progressRequestId).subscribe({
      next: response => this.handleAutoBid(response),
      error: (error: unknown) => {
        this.busy.set(false);
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
    if (response.ready) {
      this.busy.set(false);
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
      this.pollUntilReady();
      return;
    }
    this.busy.set(false);
    this.messageError.set(true);
    this.message.set('Plata nu este configurată. Contactează suportul.');
  }

  /** Downloads one auto-generated proposal file through the API (the stored local://s3:// reference is
   *  resolved server-side; browsers cannot navigate those schemes directly). */
  downloadBidFile(kind: 'TechnicalProposal' | 'BillOfQuantities'): void {
    if (this.downloading()) return;
    this.downloading.set(true);
    this.message.set('');
    this.service.downloadBidFile(this.tenderId, kind).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = kind === 'TechnicalProposal' ? 'PropunereTehnica.docx' : 'ListaCantitati.xlsx';
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
      next: status => { this.bidStatus.set(status); if (status.status !== 'BidGenerated') this.busy.set(true); },
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
}

