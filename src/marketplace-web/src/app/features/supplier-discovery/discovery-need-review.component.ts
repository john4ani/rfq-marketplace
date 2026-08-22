import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { DiscoveryNeedReview, OpportunityMatch, SupplierDiscoveryService, SupplierWebCandidate, SupplierWebDiscoveryRun } from '../../core/supplier-discovery/supplier-discovery.service';

@Component({
  selector: 'app-discovery-need-review', imports: [RouterLink, DatePipe, ReactiveFormsModule], changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`:host{display:block}.page{display:grid;gap:1rem;max-width:76rem}.card,.state{padding:1.1rem;border:1px solid var(--border);border-radius:1rem;background:var(--surface)}.row{display:flex;justify-content:space-between;gap:1rem;align-items:start}.facts,.links,.actions{display:flex;gap:.7rem;flex-wrap:wrap}.facts{color:var(--muted)}.note{padding:.75rem;border-radius:.65rem;background:var(--sand)}.web{display:grid;gap:.8rem;border-color:#d6e2d9}.badge{color:#75530c;font-weight:750}.approved{color:#176b52}.rejected{color:#8b2222}.review-form{display:grid;gap:.8rem;margin-top:1rem;padding:1rem;border:1px solid var(--border);border-radius:.75rem;background:var(--surface)}.review-form label{display:grid;gap:.3rem;font-weight:650}.review-form input{min-height:2.6rem;padding:.5rem .65rem;border:1px solid var(--border);border-radius:.45rem;background:var(--surface)}.review-form input.ng-invalid.ng-touched{border-color:#a32525}.review-form small{font-weight:400;color:var(--muted)}@media(max-width:40rem){.row{flex-direction:column}.actions button{flex:1}}`],
  template: `
  <section class="page">
    <a routerLink="/admin/supplier-discovery">← Înapoi la discovery</a>
    @if (loading()) { <section class="state" role="status">Se încarcă potrivirile și detaliile cererii…</section> }
    @else if (error()) { <section class="state" role="alert"><p>{{ error() }}</p><button type="button" (click)="reload()">Încearcă din nou</button></section> }
    @else if (review(); as data) {
      <header><p class="eyebrow">Cerere concretă</p><h1>{{ data.title }}</h1><p>{{ data.deliveryLocality }}, {{ data.county }} · termen {{ data.quotationDeadline | date:'dd.MM.yyyy, HH:mm' }}</p></header>
      <section class="card"><h2>Cererea originală</h2><p><strong>Categorie:</strong> {{ data.request.categoryName }}</p><ul>@for (line of data.request.lines; track $index) { <li>{{ line.name }} · {{ line.quantity }} {{ line.unit }}</li> }</ul></section>
      <aside class="state"><strong>Control uman obligatoriu</strong><p>O aprobare nu trimite nimic automat. Rezultatele web sunt dovezi provizorii.</p></aside>
      <section class="card web">
        <div class="row"><div><p class="eyebrow">După potrivirile interne</p><h2>Caută furnizori pe web</h2><p>Trimitem doar categoria, produsele/serviciile solicitate și zona de livrare; nu datele cumpărătorului.</p></div><button type="button" [disabled]="saving()" (click)="discover(data.rfqId)">{{ saving() ? 'Se caută…' : 'Caută furnizori pe web' }}</button></div>
        @if (webError()) { <p class="note" role="alert">{{ webError() }} <button type="button" [disabled]="saving()" (click)="discover(data.rfqId)">Încearcă din nou</button></p> }
        @if (webSuccess()) { <p class="note" role="status">{{ webSuccess() }}</p> }
        @if (webRun(); as run) {
          @if (run.status === 'EvidenceReady') {
            @if (run.failureMessage) {
              <section class="note" role="alert"><strong>Calificarea nu s-a finalizat.</strong><p>{{ run.failureMessage }}</p><button type="button" [disabled]="saving()" (click)="qualify(run.id)">Reîncearcă calificarea</button></section>
            } @else {
              <section class="note" role="status"><strong>Dovezi provizorii · neverificate</strong><p class="facts">@for (source of run.sources; track source.source) { {{ source.source }}: {{ source.resultCount }} rezultate @if (source.warning) { · {{ source.warning }} } }</p><p>Google Places ajută la găsirea firmelor locale; căutarea web adaugă dovezi despre activitate și produse. Rezultatele sunt verificate înainte de a fi adăugate.</p><button type="button" [disabled]="saving()" (click)="qualify(run.id)">Califică rezultatele</button></section>
            }
            @if (run.provisionalEvidence.length) { <details><summary>Dovezi externe găsite ({{ run.provisionalEvidence.length }})</summary>@for (evidence of run.provisionalEvidence; track evidence.companyName) { <article class="note"><strong>{{ evidence.companyName }}</strong><p>{{ evidence.locality }}, {{ evidence.county }} · {{ evidence.sources.join(' · ') }}</p><div class="links">@for (url of evidence.sourceUrls; track url) { <a [href]="url" target="_blank" rel="noreferrer">Sursă externă ↗</a> }</div></article> }</details> }
          }
          @else if (run.status === 'Failed') { <p class="note" role="alert">{{ run.failureMessage }} <button type="button" [disabled]="saving()" (click)="discover(data.rfqId)">Încearcă din nou</button></p> }
          @else {
            <h3>Rezultate găsite pe web</h3>
            @for (candidate of run.candidates; track candidate.companyName; let index = $index) {
              <article class="note"><p class="badge">{{ candidate.sources.join(' + ') }} · Neverificată</p><h3>{{ candidate.companyName }}</h3><p>{{ candidate.locality }}, {{ candidate.county }} · {{ candidate.confidence }}</p><p><strong>De ce poate fi relevantă:</strong> {{ candidate.whyRelevant }}</p><p><strong>Dovezi:</strong> {{ candidate.activityEvidence }} @if (candidate.productEvidence) { · {{ candidate.productEvidence }} }</p><div class="links">@for (url of candidate.sourceUrls; track url) { <a [href]="url" target="_blank" rel="noreferrer">Sursă externă ↗</a> }</div>
              @if (reviewingCompanyName() === candidate.companyName) {
                <form class="review-form" [formGroup]="candidateForm" (ngSubmit)="accept(run.id, index, candidate.companyName)">
                  <div><strong>Verifică datele înainte de adăugare</strong><p>Completează sau corectează doar ce lipsește. Dovezile și sursele rămân neschimbate.</p><p><strong>Categorie:</strong> {{ data.request.categoryName }} <small>— preluată automat din cerere</small></p></div>
                  <label>Denumirea firmei <input formControlName="companyName" autocomplete="organization"> @if (invalid('companyName')) { <small>Completează denumirea firmei.</small> }</label>
                  <label>Localitate <input formControlName="locality" autocomplete="address-level2"> @if (invalid('locality')) { <small>Completează localitatea.</small> }</label>
                  <label>Județ <input formControlName="county" autocomplete="address-level1"> @if (invalid('county')) { <small>Completează județul.</small> }</label>
                  <label>E-mail <input formControlName="email" type="email" autocomplete="email"> @if (invalid('email')) { <small>Introdu o adresă de e-mail validă sau lasă câmpul gol.</small> }</label>
                  <label>Telefon <input formControlName="phone" type="tel" autocomplete="tel"></label>
                  <label>Website <input formControlName="website" type="url" autocomplete="url"></label>
                  <div class="actions"><button type="submit" [disabled]="saving()">{{ saving() ? 'Se adaugă…' : 'Adaugă în Firme descoperite' }}</button><button type="button" class="secondary" [disabled]="saving()" (click)="cancelReview()">Renunță</button></div>
                </form>
              } @else { <button type="button" [disabled]="saving()" (click)="startReview(candidate)">Revizuiește și adaugă</button> }
              </article>
            } @empty { <p class="note">Nu au fost găsite firme suficient de relevante.</p> }
            @if (run.rejectedResults.length) { <details><summary>Rezultate respinse ({{ run.rejectedResults.length }})</summary>@for (rejected of run.rejectedResults; track rejected.companyName) { <p>{{ rejected.companyName }} — {{ rejected.reason }}</p> }</details> }
          }
        }
      </section>
      <h2>Firme înregistrate ({{ data.registeredCompanies.length }})</h2>
      @for (match of data.registeredCompanies; track match.id) { <article class="card"><h3>{{ match.companyName }}</h3><p class="facts">{{ match.city }}, {{ match.county }} · firmă înregistrată</p><p class="note">{{ match.matchReason }}</p></article> }
      <h2>Firme descoperite ({{ data.candidates.length }})</h2>
      @for (match of data.candidates; track match.id) { <article class="card"><div class="row"><div><h3>{{ match.companyName }}</h3><p class="facts">{{ match.city }}, {{ match.county }}</p><p class="note">{{ match.matchReason }}</p></div><div class="actions">@if (match.status === 'PendingReview') { <button type="button" [disabled]="saving()" (click)="decide(match, true)">Aprobă pentru contact</button><button type="button" class="secondary" [disabled]="saving()" (click)="decide(match, false)">Respinge</button> } @else { <strong [class.approved]="match.status === 'Approved'" [class.rejected]="match.status === 'Rejected'">{{ statusLabel(match.status) }}</strong> }</div></div></article> } @empty { <section class="state"><strong>Nu există firme descoperite relevante pentru această cerere.</strong></section> }
    }
  </section>`
})
export class DiscoveryNeedReviewComponent {
  private readonly api = inject(SupplierDiscoveryService); private readonly route = inject(ActivatedRoute);
  readonly review = signal<DiscoveryNeedReview | null>(null); readonly webRun = signal<SupplierWebDiscoveryRun | null>(null);
  readonly loading = signal(true); readonly saving = signal(false); readonly error = signal(''); readonly webError = signal(''); readonly webSuccess = signal('');
  readonly reviewingCompanyName = signal<string | null>(null);
  readonly candidateForm = new FormGroup({
    companyName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(2), Validators.maxLength(200)] }),
    locality: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(2), Validators.maxLength(120)] }),
    county: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(2), Validators.maxLength(120)] }),
    email: new FormControl('', { nonNullable: true, validators: [Validators.email, Validators.maxLength(254)] }),
    phone: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(40)] }),
    website: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(500)] })
  });
  constructor() { this.reload(); }
  decide(match: OpportunityMatch, approved: boolean): void { this.saving.set(true); this.api.reviewMatch(match.id, approved).subscribe({ next: () => { this.saving.set(false); this.reload(); }, error: () => { this.saving.set(false); this.error.set('Decizia nu a putut fi salvată. Încearcă din nou.'); } }); }
  discover(rfqId: string): void { this.saving.set(true); this.webError.set(''); this.webSuccess.set(''); this.cancelReview(); this.api.discoverOnWeb(rfqId).subscribe({ next: run => { this.webRun.set(run); this.saving.set(false); }, error: () => { this.saving.set(false); this.webError.set('Căutarea nu a putut fi pornită. Reîncarcă pagina, apoi încearcă din nou.'); } }); }
  qualify(runId: string): void { this.saving.set(true); this.api.qualifyWebDiscovery(runId).subscribe({ next: run => { this.webRun.set(run); this.saving.set(false); }, error: () => { this.saving.set(false); this.error.set('Nu am putut califica rezultatele găsite pe web.'); } }); }
  startReview(candidate: SupplierWebCandidate): void { this.webSuccess.set(''); this.reviewingCompanyName.set(candidate.companyName); this.candidateForm.reset({ companyName: candidate.companyName, locality: candidate.locality, county: candidate.county, email: candidate.email ?? '', phone: candidate.phone ?? '', website: candidate.website ?? '' }); }
  cancelReview(): void { this.reviewingCompanyName.set(null); this.candidateForm.reset(); }
  invalid(controlName: keyof typeof this.candidateForm.controls): boolean { const control = this.candidateForm.controls[controlName]; return control.invalid && control.touched; }
  accept(runId: string, index: number, sourceCompanyName: string): void { if (this.candidateForm.invalid) { this.candidateForm.markAllAsTouched(); return; } const reviewedDetails = this.candidateForm.getRawValue(); this.saving.set(true); this.webSuccess.set(''); this.api.acceptWebCandidate(runId, index, [], sourceCompanyName, reviewedDetails).subscribe({ next: ({ candidate }) => { this.saving.set(false); this.webRun.update(run => run ? { ...run, candidates: run.candidates.filter(item => item.companyName !== sourceCompanyName) } : run); this.cancelReview(); this.webSuccess.set(`Firma „${candidate.companyName}” a fost adăugată în Firme descoperite și asociată acestei cereri.`); this.reload(); }, error: () => { this.saving.set(false); this.error.set('Firma nu a putut fi adăugată. Verifică datele și încearcă din nou.'); } }); }
  reload(): void { const id = this.review()?.rfqId ?? this.route.snapshot.paramMap.get('rfqId'); if (!id) { this.error.set('Cererea nu este disponibilă.'); this.loading.set(false); return; } this.loading.set(true); this.error.set(''); this.api.needReview(id).subscribe({ next: item => { this.review.set(item); this.loading.set(false); }, error: () => { this.error.set('Potrivirile nu au putut fi încărcate.'); this.loading.set(false); } }); }
  statusLabel(status: string): string { return status === 'Approved' ? 'Aprobată pentru contact' : status === 'Rejected' ? 'Respinsă pentru această cerere' : 'În așteptarea aprobării'; }
}
