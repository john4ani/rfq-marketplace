import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, ElementRef, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { of, switchMap, tap } from 'rxjs';
import { AssistedDraft } from '../../core/rfqs/assisted-intake.service';
import { ConstructionIntakeResult, ConstructionIntakeService } from '../../core/rfqs/construction-intake.service';
import { RfqDraft, RfqService, UnitOfMeasure } from '../../core/rfqs/rfq.service';
import { CategoryOption, SupplierSettingsService } from '../../core/suppliers/supplier-settings.service';
import { ROMANIAN_COUNTIES } from '../../shared/romanian-counties';
import { unitLabel } from '../../shared/unit-label';
import { CONSTRUCTION_VERTICAL_ID } from '../../shared/verticals';

@Component({
  selector: 'app-construction-intake',
  imports: [FormsModule, RouterLink],
  template: `<main class="intake-shell">
    <a class="back" routerLink="/dashboard">← Înapoi acasă</a>
    <header><div class="assistant-title"><span aria-hidden="true">▲</span><div><p class="eyebrow">Proiect de construcții</p><h1>Ce vrei să construiești?</h1></div></div>
      <p>Descrie proiectul. Te ajutăm să-l structurăm și să pregătim achizițiile necesare. Nimic nu se trimite fără confirmarea ta.</p></header>
    @if (checkingAvailability()) {
      <section class="start-card" aria-live="polite">Se pregătește asistentul de proiect…</section>
    } @else if (aiUnavailable()) {
      <section class="start-card" role="alert"><strong>Asistentul de proiect este indisponibil momentan.</strong>
        <p>Descrierea ta a fost păstrată. Poți încerca din nou sau poți completa proiectul manual.</p>
        <div class="actions"><a routerLink="/rfqs/new/advanced" [queryParams]="manualQueryParams()">Continuă manual</a>
          <button type="button" (click)="retryAvailability()">Încearcă din nou</button></div>
      </section>
    } @else if (!result()) {
      <section class="start-card"><label for="project">Descrie proiectul de construcții</label>
        <textarea id="project" [(ngModel)]="input" rows="7" maxlength="4000" placeholder="Exemplu: Vreau un acoperiș nou pentru o casă 10×12, în Iași..."></textarea>
        <div class="starter-note"><span aria-hidden="true">▲</span> Punem o singură întrebare pe rând, doar ce contează pentru necesar.</div>
        <div class="actions"><a routerLink="/rfqs/new/advanced" [queryParams]="{entryMode:'construction'}">Prefer să completez manual</a>
          <button type="button" (click)="start()" [disabled]="busy() || input.trim().length < 10">{{ busy() ? 'Analizăm proiectul…' : 'Continuă' }}</button></div>
      </section>
    } @else {
      <div class="workspace" [class.reviewing]="!result()?.clarification">
        <section class="conversation">
          <div class="user-message"><span>Ai descris</span><p>{{ input }}</p></div>
          @for (entry of result()?.answers; track $index) {
            <div class="history"><span>Clarificare {{ $index + 1 }}</span><p class="history-question">{{ entry.question }}</p><p><strong>Răspunsul tău:</strong> {{ entry.answer }}</p></div>
          }
          @if (result()?.clarification; as clarification) {
            <div class="question"><span>O clarificare despre proiect</span><h2>{{ clarification.question }}</h2>
              @if (clarification.choices.length) { <div class="choices">@for (choice of clarification.choices; track choice) { <button type="button" (click)="answer=choice; clarify()" [disabled]="busy()">{{ choice }}</button> }</div> }
              @else { <div class="answer"><input [(ngModel)]="answer" (keyup.enter)="clarify()" placeholder="Scrie răspunsul"><button type="button" (click)="clarify()" [disabled]="busy() || !answer.trim()">Răspunde</button></div> }
            </div>
          } @else { <div class="ready"><strong>Necesarul proiectului este pregătit pentru verificare</strong><p>Nu este publicat automat. Îl poți corecta înainte să-l salvezi.</p></div> }
        </section>
        <aside class="understanding">
          @if (result()?.projectSummary) { <section class="preview-group project-understanding"><h3>Ce am înțeles despre proiect</h3><p>{{ result()?.projectSummary }}</p></section> }
          @if (result()?.clarification) {
            <div class="understanding-heading"><span aria-hidden="true">✓</span><div><p class="eyebrow">Pe măsură ce răspunzi</p><h2>Necesarul de până acum</h2></div></div>
            <dl class="known-facts">
              <div><dt>Proiect</dt><dd>{{ draft.title || 'Încă îl clarificăm' }}</dd></div>
              <div><dt>Categorie</dt><dd>{{ categoryName(draft.primaryCategoryId) }}</dd></div>
              <div><dt>Livrare</dt><dd>{{ draft.deliveryLocality || draft.county ? (draft.deliveryLocality + (draft.county ? ', ' + draft.county : '')) : 'De stabilit' }}</dd></div>
              <div><dt>Poziții propuse</dt><dd>{{ draft.lines.length || 'În curs de pregătire' }}</dd></div>
            </dl>
            @if(result()?.assumptions?.length){<details open><summary>Presupuneri de confirmat</summary><ul>@for(item of result()?.assumptions;track item){<li>{{item}}</li>}</ul></details>}
            <p class="advanced-note">Răspunde la întrebarea din stânga. Vei putea verifica și corecta necesarul complet înainte de salvare.</p>
          } @else {
            <div class="understanding-heading"><span aria-hidden="true">✓</span><div><p class="eyebrow">Confirmare umană</p><h2>Verifică necesarul</h2></div></div>
            <p class="review-note">Estimările sunt asistență comercială, nu calcule inginerești certificate. Verifică valorile importante înainte să salvezi.</p>
            <section class="preview-group"><h3>Despre necesar</h3>
              <label>Titlu *<input [(ngModel)]="draft.title"></label>
              <label>Categorie *<select [(ngModel)]="draft.primaryCategoryId"><option [ngValue]="null">Alege categoria</option>@for(category of leafCategories();track category.id){<option [value]="category.id">{{category.name}}</option>}</select></label>
              <label>Descriere<textarea [(ngModel)]="draft.description" rows="3"></textarea></label></section>
            <section class="preview-group"><h3>Livrare</h3><div class="two"><label>Localitate *<input [(ngModel)]="draft.deliveryLocality"></label><label>Județ *<input [(ngModel)]="draft.county"></label></div>
              <label>Adresă de livrare *<input [(ngModel)]="draft.deliveryAddress"></label></section>
            <section class="preview-group"><h3>Materiale sau lucrări <span>{{ draft.lines.length }}</span></h3>
              @for(line of draft.lines;track $index){<div class="line"><select [(ngModel)]="line.kind"><option value="Product">Material</option><option value="Service">Lucrare</option></select><input class="name" [(ngModel)]="line.name" aria-label="Denumire"><input type="number" min="0.01" [(ngModel)]="line.quantity" aria-label="Cantitate"><select [(ngModel)]="line.unit"><option [ngValue]="null">Unitate</option>@for(unit of units;track unit){<option [value]="unit">{{unitLabel(unit)}}</option>}</select></div>}</section>
            @if(result()?.assumptions?.length){<details open><summary>Presupuneri</summary><ul>@for(item of result()?.assumptions;track item){<li>{{item}}</li>}</ul></details>}
            @if(result()?.uncertainties?.length){<details open class="uncertain"><summary>De verificat</summary><ul>@for(item of result()?.uncertainties;track item){<li>{{item}}</li>}</ul></details>}
            <div class="approval-actions">
              <button class="save-draft" type="button" (click)="approve(false)" [disabled]="busy()">Salvează ciorna</button>
              <button class="approve" type="button" (click)="approve(true)" [disabled]="busy()">{{busy()?'Se trimite…':'Trimite cererea'}}</button>
            </div>
            <a class="advanced" routerLink="/rfqs/new/advanced" [queryParams]="manualQueryParams()">Prefer formularul complet? Continuă manual</a>
          }
        </aside>
      </div>
    }
    @if(message()){<p class="message" [class.error]="hasError()" role="alert">{{message()}}</p>}
  </main>`,
  styleUrl: './assisted-intake.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ConstructionIntakeComponent {
  private readonly service = inject(ConstructionIntakeService); private readonly rfqService = inject(RfqService); private readonly categoriesService = inject(SupplierSettingsService);
  private readonly route = inject(ActivatedRoute); private readonly router = inject(Router); private readonly host = inject(ElementRef<HTMLElement>);
  readonly result = signal<ConstructionIntakeResult | null>(null); readonly categories = signal<CategoryOption[]>([]); readonly busy = signal(false);
  readonly message = signal(''); readonly hasError = signal(false); readonly checkingAvailability = signal(true); readonly aiUnavailable = signal(false);
  input = ''; answer = ''; draft: AssistedDraft = this.emptyDraft();
  readonly units: UnitOfMeasure[] = ['Piece', 'Pack', 'Bag', 'Kilogram', 'Tonne', 'Metre', 'SquareMetre', 'CubicMetre', 'Litre', 'Pallet', 'Other'];
  readonly unitLabel = unitLabel;
  constructor() {
    const id = this.route.snapshot.queryParamMap.get('session');
    const description = this.route.snapshot.queryParamMap.get('description')?.trim() ?? '';
    this.input = description;
    this.checkAvailability(id, description);
  }
  leafCategories() {
    const all = this.categories(), parents = new Set(all.flatMap(x => x.parentId ? [x.parentId] : []));
    const selected = new Set([this.draft.primaryCategoryId, ...this.draft.lines.map(line => line.categoryId)].filter((x): x is string => !!x));
    return all.filter(x => !parents.has(x.id) && (x.verticalId === CONSTRUCTION_VERTICAL_ID || selected.has(x.id)));
  }
  categoryName(id: string | null) { return this.categories().find(category => category.id === id)?.name ?? (id ? 'Categorie selectată' : 'De stabilit'); }
  start() { if (this.input.trim().length < 10) return; this.busy.set(true); this.clear(); this.service.start(this.input.trim()).subscribe({ next: v => { this.apply(v); void this.router.navigate([], { queryParams: { session: v.sessionId }, replaceUrl: true }); }, error: e => this.fail(e) }); }
  clarify() { if (!this.answer.trim() || !this.result()) return; this.busy.set(true); this.clear(); this.service.clarify(this.result()!.sessionId, this.answer.trim()).subscribe({ next: v => { this.answer = ''; this.apply(v); }, error: e => this.fail(e) }); }
  approve(publish = false) { const invalid = this.validate(); if (invalid) { this.message.set(invalid); this.hasError.set(true); this.scrollError(); return; } this.busy.set(true); this.clear(); let draftSaved = false; this.service.approve(this.result()!.sessionId, this.toRfqDraft()).pipe(tap(() => draftSaved = true), switchMap(rfq => publish ? this.rfqService.publish(rfq.id) : of(rfq))).subscribe({ next: rfq => void this.router.navigate(publish ? ['/rfqs', rfq.id, 'offers'] : ['/rfqs', rfq.id, 'edit'], publish ? undefined : { queryParams: { assisted: 'saved' } }), error: e => { if (publish && draftSaved) { this.busy.set(false); this.message.set('Ciorna a fost salvată, dar publicarea nu a reușit. Încearcă din nou; informațiile nu s-au pierdut.'); this.hasError.set(true); this.scrollError(); return; } this.fail(e); } }); }
  private apply(value: ConstructionIntakeResult) { this.result.set(value); this.input = value.initialInput; this.draft = structuredClone(value.draft); this.draft.county = this.canonicalCounty(this.draft.county); this.busy.set(false); }
  private validate() { if (!this.draft.title.trim()) return 'Titlul este obligatoriu.'; if (!this.draft.primaryCategoryId) return 'Selectează cel puțin o categorie.'; if (!this.draft.deliveryAddress.trim()) return 'Completează adresa de livrare.'; if (!this.draft.deliveryLocality.trim() || !this.draft.county.trim()) return 'Completează localitatea și județul.'; if (this.draft.lines.some(x => !x.name.trim() || !x.quantity || x.quantity <= 0 || !x.unit)) return 'Verifică denumirea, cantitatea și unitatea fiecărei poziții.'; return ''; }
  private toRfqDraft(): RfqDraft { const categoryIds = [this.draft.primaryCategoryId!]; return { source: 'Assisted', inputType: 'ProjectDescription', needType: 'Project', projectTemplateId: null, categoryIds, title: this.draft.title, projectName: this.draft.projectName, description: this.draft.description, primaryCategoryId: this.draft.primaryCategoryId!, deliveryAddress: this.draft.deliveryAddress, deliveryLocality: this.draft.deliveryLocality, county: this.draft.county, supplierSearchRadiusKm: this.draft.supplierSearchRadiusKm, latitude: null, longitude: null, preferredDeliveryDate: this.draft.preferredDeliveryDate, quotationDeadline: this.draft.quotationDeadline, alternativeProductsAccepted: this.draft.alternativeProductsAccepted, estimatedBudget: this.draft.estimatedBudget, buyerNotes: this.draft.buyerNotes, allowSplit: this.draft.allowSplit, deliveryRequired: this.draft.deliveryRequired, items: this.draft.lines.map(x => ({ productName: x.name, manufacturerOrBrand: null, specification: x.specification, quantity: x.quantity!, unit: x.unit!, alternativesAccepted: x.alternativesAccepted, buyerNotes: null, categoryId: null, kind: x.kind })) }; }
  retryAvailability() { this.checkingAvailability.set(true); this.aiUnavailable.set(false); this.checkAvailability(this.result()?.sessionId ?? this.route.snapshot.queryParamMap.get('session'), this.input); }
  manualQueryParams() { return { entryMode: 'construction', ...(this.input.trim() ? { description: this.input.trim() } : {}) }; }
  private checkAvailability(sessionId: string | null, description: string) {
    this.service.availability().subscribe({
      next: value => {
        this.checkingAvailability.set(false);
        if (!value.enabled || !value.modelAvailable) { this.aiUnavailable.set(true); return; }
        this.categoriesService.getCategories().subscribe({ next: v => this.categories.set(v) });
        if (sessionId) { this.busy.set(true); this.service.get(sessionId).subscribe({ next: v => this.apply(v), error: e => this.fail(e) }); return; }
        if (description.length >= 10) this.start();
      }, error: () => { this.checkingAvailability.set(false); this.aiUnavailable.set(true); }
    });
  }
  private fail(error: HttpErrorResponse) { this.busy.set(false); if (error.status === 503) { this.aiUnavailable.set(true); return; } const detail = error.error?.errors?.intake?.[0] ?? error.error?.detail ?? 'Nu am putut procesa proiectul. Încearcă din nou.'; this.message.set(detail); this.hasError.set(true); this.scrollError(); }
  private clear() { this.message.set(''); this.hasError.set(false); }
  private scrollError() { setTimeout(() => this.host.nativeElement.querySelector('[role="alert"]')?.scrollIntoView({ behavior: 'smooth', block: 'center' })); }
  private canonicalCounty(value: string) { const key = this.fold(value); return ROMANIAN_COUNTIES.find(county => this.fold(county) === key) ?? value; }
  private fold(value: string) { return value.normalize('NFD').replace(/[̀-ͯ]/g, '').toLocaleLowerCase('ro-RO'); }
  private emptyDraft(): AssistedDraft { return { inputType: 'ProjectDescription', title: '', projectName: null, description: null, primaryCategoryId: null, deliveryAddress: '', deliveryLocality: '', county: '', supplierSearchRadiusKm: 30, preferredDeliveryDate: null, quotationDeadline: new Date(Date.now() + 259200000).toISOString(), alternativeProductsAccepted: true, allowSplit: false, deliveryRequired: false, estimatedBudget: null, buyerNotes: null, lines: [] }; }
}
