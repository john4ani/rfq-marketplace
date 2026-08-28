import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, ElementRef, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ProfileService, RepresentedCompany } from '../../core/profiles/profile.service';
import { SeapClarificationTurn, SeapService, SeapTenderMatchResult } from '../../core/seap/seap.service';

// SEAP Tender Match onboarding chat: reuses the assisted-intake conversational pattern ("Ce ai nevoie?") but
// produces rigid SEAP filters (CPV prefixes + counties) instead of a Procurement Need. The flow is
// describe → one clarification at a time → synthesis card → human confirmation → save on the company profile
// → transition to the Anunțuri SEAP feed.
@Component({
  selector: 'app-seap-onboarding-chat',
  imports: [FormsModule, RouterLink],
  template: `<main class="intake-shell">
    <a class="back" routerLink="/seap/tenders">← Anunțuri SEAP</a>
    <header><div class="assistant-title"><span aria-hidden="true">⚑</span><div><p class="eyebrow">Potrivire anunțuri</p><h1>Descrie ce licitații cauți</h1></div></div>
      <p>Spune în cuvinte simple ce anunțuri te interesează (domeniu, zonă). Transformăm descrierea în filtre: coduri CPV și județe.</p></header>

    @if (checkingAvailability()) {
      <section class="start-card" aria-live="polite">Se pregătește asistentul…</section>
    } @else if (aiUnavailable()) {
      <section class="start-card" role="alert"><strong>Asistentul de potrivire este indisponibil momentan.</strong>
        <p>Poți configura filtrele manual din pagina Anunțuri SEAP.</p>
        <div class="actions"><a routerLink="/seap/tenders">Deschide Anunțuri SEAP</a>
          <button type="button" (click)="retryAvailability()">Încearcă din nou</button></div>
      </section>
    } @else if (!result()) {
      <section class="start-card"><label for="match">Ce licitații te interesează?</label>
        <textarea id="match" [(ngModel)]="input" rows="7" maxlength="4000" placeholder="Exemplu: Lucrări de drumuri și poduri în zona Moldovei..."></textarea>
        <div class="starter-note"><span aria-hidden="true">⚑</span> Extragem doar domeniul (coduri CPV) și județele. Nimic nu se salvează fără acordul tău.</div>
        <div class="actions"><a routerLink="/seap/tenders">Prefer să filtrez manual</a>
          <button type="button" (click)="start()" [disabled]="busy() || input.trim().length < 10">{{ busy() ? 'Analizăm descrierea…' : 'Continuă' }}</button></div>
      </section>
    }
    @else {
      <div class="workspace" [class.reviewing]="!result()?.clarificationNeeded">
        <section class="conversation">
          <div class="user-message"><span>Ai descris</span><p>{{ input }}</p></div>
          @for (entry of turns(); track $index) {
            <div class="history"><span>Clarificare {{ $index + 1 }}</span><p class="history-question">{{ entry.question }}</p><p><strong>Răspunsul tău:</strong> {{ entry.answer }}</p></div>
          }
          @if (result()?.clarificationNeeded) {
            <div class="question"><span>O clarificare</span><h2>{{ result()?.clarificationQuestion }}</h2>
              <div class="answer"><input [(ngModel)]="answer" (keyup.enter)="clarify()" placeholder="Scrie răspunsul"><button type="button" (click)="clarify()" [disabled]="busy() || !answer.trim()">Răspunde</button></div>
            </div>
          } @else {
            <div class="ready"><strong>Filtre pregătite pentru confirmare</strong><p>Nu le aplicăm automat. Verifică rezultatul înainte de salvare.</p></div>
          }
          @if (message()) { <p class="message" [class.error]="hasError()" role="alert">{{ message() }}</p> }
        </section>
        <aside class="understanding">
          @if (result()?.clarificationNeeded) {
            <div class="understanding-heading"><span aria-hidden="true">✓</span><div><p class="eyebrow">Pe măsură ce răspunzi</p><h2>Ce am înțeles până acum</h2></div></div>
            <p class="review-note">Răspunde la întrebarea din stânga pentru a obține filtre mai precise.</p>
          } @else {
            <div class="understanding-heading"><span aria-hidden="true">✓</span><div><p class="eyebrow">Confirmare umană</p><h2>Verifică filtrele</h2></div></div>
            @if (result()?.summaryText; as summary) { <p class="review-note">{{ summary }}</p> }
            <section class="preview-group"><h3>Coduri CPV <span>{{ result()?.extractedCpvPrefixes?.length ?? 0 }}</span></h3>
              @if (result()?.extractedCpvPrefixes?.length) { <div class="chips">@for (prefix of result()?.extractedCpvPrefixes; track prefix) { <span class="chip">{{ prefix }}</span> }</div> }
              @else { <p class="muted">Niciun prefix CPV extras.</p> }
            </section>
            <section class="preview-group"><h3>Județe <span>{{ result()?.extractedCounties?.length ?? 0 }}</span></h3>
              @if (result()?.extractedCounties?.length) { <div class="chips">@for (county of result()?.extractedCounties; track county) { <span class="chip">{{ county }}</span> }</div> }
              @else { <p class="muted">Toată țara.</p> }
            </section>
            <div class="approval-actions">
              @if (company(); as company) {
                <button type="button" class="approve" [disabled]="saving()" (click)="confirm()">{{ saving() ? 'Se salvează…' : 'Salvează filtrele' }}</button>
                <p class="advanced-note">Salvăm filtrele pe firma <strong>{{ company.name }}</strong> și deschidem Anunțurile SEAP filtrate.</p>
              } @else {
                <a class="approve link" routerLink="/account/company/new">Adaugă firma pentru a salva filtrele</a>
                <p class="advanced-note">Chat-ul funcționează și fără firmă; salvarea filtrelor cere o firmă.</p>
              }
            </div>
          }
        </aside>
      </div>
    }
  </main>`,
  styleUrl: './seap-onboarding-chat.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})


export class SeapOnboardingChatComponent {
  private readonly service = inject(SeapService);
  private readonly profiles = inject(ProfileService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly host = inject(ElementRef<HTMLElement>);

  readonly checkingAvailability = signal(true);
  readonly aiUnavailable = signal(false);
  readonly busy = signal(false);
  readonly saving = signal(false);
  readonly turns = signal<SeapClarificationTurn[]>([]);
  readonly result = signal<SeapTenderMatchResult | null>(null);
  readonly company = signal<RepresentedCompany | null>(null);
  readonly message = signal('');
  readonly hasError = signal(false);
  input = '';
  answer = '';

  constructor() {
    this.input = this.route.snapshot.queryParamMap.get('description') ?? '';
    this.profiles.getMyCompanies().subscribe({
      next: companies => this.company.set(companies[0] ?? null),
      error: () => this.company.set(null)
    });
    this.checkAvailability(this.input);
  }

  start(): void {
    if (this.input.trim().length < 10 || this.busy()) return;
    this.busy.set(true);
    this.clear();
    this.service.matchTenders(this.input.trim(), this.turns()).subscribe({
      next: value => this.apply(value),
      error: error => this.fail(error)
    });
  }

  clarify(): void {
    const result = this.result();
    if (!result?.clarificationNeeded || !this.answer.trim() || this.busy()) return;
    this.turns.update(turns => [...turns, { question: result.clarificationQuestion ?? '', answer: this.answer.trim() }]);
    this.answer = '';
    this.busy.set(true);
    this.clear();
    this.service.matchTenders(this.input.trim(), this.turns()).subscribe({
      next: value => this.apply(value),
      error: error => this.fail(error)
    });
  }

  confirm(): void {
    const company = this.company();
    const result = this.result();
    if (!company || !result || result.clarificationNeeded || this.saving()) return;
    this.saving.set(true);
    this.clear();
    this.service.saveMatchProfile(company.id, result.extractedCpvPrefixes, result.extractedCounties).subscribe({
      next: () => void this.router.navigate(['/seap/tenders']),
      error: error => {
        this.saving.set(false);
        const problem = (error as HttpErrorResponse).error;
        this.message.set(problem?.errors?.match?.[0] ?? problem?.detail ?? 'Filtrele nu au putut fi salvate.');
        this.hasError.set(true);
        this.scrollError();
      }
    });
  }

  retryAvailability(): void {
    this.checkingAvailability.set(true);
    this.aiUnavailable.set(false);
    this.checkAvailability(this.input);
  }

  private checkAvailability(description: string): void {
    this.service.matchTenderAvailability().subscribe({
      next: value => {
        this.checkingAvailability.set(false);
        if (!value.enabled || !value.modelAvailable) { this.aiUnavailable.set(true); return; }
        if (description.trim().length >= 10) this.start();
      },
      error: () => { this.checkingAvailability.set(false); this.aiUnavailable.set(true); }
    });
  }

  private apply(value: SeapTenderMatchResult): void {
    this.result.set(value);
    this.busy.set(false);
  }

  private fail(error: HttpErrorResponse): void {
    this.busy.set(false);
    if (error.status === 503) { this.aiUnavailable.set(true); return; }
    const problem = error.error;
    this.message.set(problem?.errors?.match?.[0] ?? problem?.detail ?? 'Nu am putut procesa descrierea. Încearcă din nou.');
    this.hasError.set(true);
    this.scrollError();
  }

  private clear(): void { this.message.set(''); this.hasError.set(false); }
  private scrollError(): void {
    setTimeout(() => this.host.nativeElement.querySelector('[role="alert"]')?.scrollIntoView({ behavior: 'smooth', block: 'center' }));
  }
}
