import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, ElementRef, inject, QueryList, signal, ViewChildren } from '@angular/core';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { switchMap } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { ProfileService } from '../../core/profiles/profile.service';
import { AssistedIntakeResult, AssistedIntakeService } from '../../core/rfqs/assisted-intake.service';
import { ProcurementInputType, ProcurementNeedSource, ProcurementNeedType, RequirementKind, Rfq, RfqDraft, RfqService, UnitOfMeasure } from '../../core/rfqs/rfq.service';
import { CategoryOption, ProjectTemplateOption, SupplierSettingsService } from '../../core/suppliers/supplier-settings.service';
import { ROMANIAN_COUNTIES } from '../../shared/romanian-counties';
import { CONSTRUCTION_VERTICAL_ID, GENERAL_PROCUREMENT_VERTICAL_ID } from '../../shared/verticals';

@Component({
  selector: 'app-rfq-editor',
  imports: [ReactiveFormsModule, RouterLink],
  template: `<main class="editor-shell">
    <a class="back" routerLink="/dashboard">← Înapoi la cont</a>
    <header><p class="eyebrow">Cerere nouă</p><h1>{{ rfqId ? 'Continuă cererea' : assistedSessionId ? 'Verifică cererea pregătită de AI' : 'Spune-ne mai multe detalii' }}</h1>
      <p>Completează doar ce contează pentru furnizor. Vei verifica totul înainte să trimiți.</p></header>
    @if (message()) { <p #pageMessage class="message" [class.error]="hasError()" role="alert">{{ message() }}</p> }
    @if (loading()) { <p role="status">Se încarcă cererea…</p> }
    <form [formGroup]="form" (ngSubmit)="save(false)">
      <section><h2>1. Despre necesar</h2>
        <label>Titlu clar *<input formControlName="title" maxlength="180" placeholder="Exemplu: Materiale pentru renovare apartament">
          <small>Spune pe scurt furnizorilor ce dorești să cumperi.</small>
          @if (form.controls.title.touched && form.controls.title.invalid) { <small class="field-error">Completează titlul cererii.</small> }</label>
        <div class="two"><label>Nume proiect<input formControlName="projectName" placeholder="Opțional"></label>
          @if(constructionEntryMode()){<label>Șablon de construcții<select formControlName="projectTemplateId" (change)="applyProjectTemplate()"><option value="">Fără șablon</option>@for(template of projectTemplates();track template.id){<option [value]="template.id">{{template.name}}</option>}</select><small>Folosește-l numai pentru un proiect de construcții; categoriile rămân editabile.</small></label>}</div>
        <fieldset><legend>Categoriile necesarului *</legend><p class="help">Alege una sau mai multe categorii, indiferent de tipul necesarului.</p>@if(categoryLoadFailed()){<div class="category-load-error" role="alert"><span>Categoriile nu sunt disponibile momentan.</span><button type="button" class="secondary" (click)="loadCategories()">Încearcă din nou</button></div>}@else{<div class="category-choice-grid">
          @for(category of selectableCategories();track category.id){<label [class.selected]="selectedCategoryIds().includes(category.id)"><input type="checkbox" [checked]="selectedCategoryIds().includes(category.id)" (change)="toggleNeedCategory(category.id)">{{category.name}}</label>}
        </div>}@if(!selectedCategoryIds().length&&!categoryLoadFailed()){<small class="field-error">Selectează cel puțin o categorie.</small>}</fieldset>
        <label>Descriere<textarea formControlName="description" rows="3" placeholder="Contextul proiectului sau cerințe generale"></textarea></label>
      </section>

      <section><div class="section-title"><div><h2>2. Produse sau servicii individuale <span class="optional">Opțional</span></h2>
        <p>Adaugă fiecare nevoie separat. Categoria poziției o direcționează către furnizorii specializați, dacă există.</p></div></div>
        <fieldset class="split-choice"><legend>Cum poate fi îndeplinită cererea?</legend>
          <label [class.selected]="!form.controls.allowSplit.value"><input type="radio" formControlName="allowSplit" [value]="false">
            <span><strong>Un singur furnizor</strong><small>Un furnizor trebuie să poată acoperi întreaga listă.</small></span></label>
          <label [class.selected]="form.controls.allowSplit.value"><input type="radio" formControlName="allowSplit" [value]="true">
            <span><strong>Permite împărțirea</strong><small>Dacă nimeni nu acoperă tot, pozițiile merg către furnizori specializați.</small></span></label>
        </fieldset>
        @if (items.length === 0) {
          <div class="optional-placeholder"><strong>Poți continua cu o cerere generală.</strong>
            <span>Descrierea și categoriile necesarului sunt suficiente dacă nu ai încă o listă detaliată.</span></div>
        } @else {
          <details><summary>Lipește mai multe poziții</summary>
            <p class="help">Câte un produs sau serviciu pe rând: denumire, cantitate și unitate, separate prin Tab sau „;”. Exemplu: <code>Filtru ulei;2;Bucată</code></p>
            <textarea [formControl]="pastedLines" rows="5" placeholder="Filtru ulei&#9;2&#9;Bucată&#10;Peleți&#9;6&#9;Tonă"></textarea>
            <button class="secondary" type="button" (click)="importLines()">Adaugă lista</button>
            @if (importMessage()) { <p [class.error]="importError()">{{ importMessage() }}</p> }
          </details>
          <div formArrayName="items" class="items">
            @for (item of items.controls; track $index; let index = $index) {
              <details class="item" [formGroupName]="index" [open]="expandedItemIndex() === index"
                (toggle)="onItemToggle(index, $event)">
                <summary><span class="item-index">{{ index + 1 }}</span><span class="item-summary">
                  <strong>{{ item.controls['productName'].value || 'Poziție nouă' }}</strong>
                  <small>{{ kindLabel(item.controls['kind'].value) }} · {{ item.controls['quantity'].value }} {{ unitLabel(item.controls['unit'].value) }}
                    · {{ categoryName(item.controls['categoryId'].value) }}</small></span><span class="collapse-hint">Detalii</span></summary>
                <div class="item-fields">
                  <button class="remove" type="button" (click)="removeItem(index)" aria-label="Șterge poziția">Șterge</button>
                  <div class="two"><label>Tip poziție *<select formControlName="kind"><option value="Product">Produs</option><option value="Service">Serviciu</option></select></label>
                    <label>Denumire produs / serviciu *<input #productNameInput formControlName="productName" placeholder="Exemplu: Ciment 40 kg sau montaj">
                    @if (item.controls['productName'].touched && item.controls['productName'].invalid) { <small class="field-error">Completează denumirea poziției {{ index + 1 }}.</small> }</label>
                    <label>Categoria poziției *<select formControlName="categoryId"><option value="">Alege categoria</option>
                      @for (category of selectedCategories(); track category.id) { <option [value]="category.id">{{ category.name }}</option> }</select>
                      <small>Poziția rămâne grupată în categoria relevantă a necesarului.</small></label></div>
                  <div class="three"><label>Cantitate *<input type="number" min="0.01" step="0.01" formControlName="quantity">
                    @if (item.controls['quantity'].touched && item.controls['quantity'].invalid) { <small class="field-error">Introdu o cantitate mai mare decât 0.</small> }</label>
                    <label>Unitate *<select formControlName="unit">@for (unit of units; track unit.value) { <option [value]="unit.value">{{ unit.label }}</option> }</select></label>
                    <label>Marcă / producător<input formControlName="manufacturerOrBrand"></label></div>
                  <label>Specificații<input formControlName="specification" placeholder="Dimensiuni, clasă, culoare…"></label>
                  <label class="check"><input type="checkbox" formControlName="alternativesAccepted">Accept produse echivalente pentru această poziție</label>
                </div>
              </details>
            }
          </div>
        }
        <button class="add-item" type="button" (click)="addItem()">{{ items.length === 0 ? '+ Adaugă prima poziție' : '+ Adaugă o poziție nouă' }}</button>
        @if (itemMessage()) { <p class="message error" role="alert">{{ itemMessage() }}</p> }
      </section>

      <section><h2>3. Livrare și termen</h2>
        <label class="check"><input type="checkbox" formControlName="deliveryRequired">
          Solicit ca transportul până la adresă să fie inclus în ofertă</label>
        <p class="help">Dacă debifezi, ridicarea sau transportul se organizează separat și cererea păstrează fluxul actual după selectarea ofertei.</p>
        <label>Adresa de livrare *<input formControlName="deliveryAddress" placeholder="Stradă, număr și detalii de acces">
          @if (form.controls.deliveryAddress.touched && form.controls.deliveryAddress.invalid) { <small class="field-error">Completează adresa de livrare.</small> }</label>
        <div class="two"><label>Localitate *<input formControlName="deliveryLocality">
          @if (form.controls.deliveryLocality.touched && form.controls.deliveryLocality.invalid) { <small class="field-error">Completează localitatea.</small> }</label>
          <label>Județ *<select formControlName="county"><option value="">Alege județul</option>@for (county of counties; track county) { <option [value]="county">{{ county }}</option> }</select>
            @if (form.controls.county.touched && form.controls.county.invalid) { <small class="field-error">Selectează județul.</small> }</label></div>
        <label>Rază de căutare a furnizorilor (km) *<input type="number" min="1" max="1000" formControlName="supplierSearchRadiusKm">
          <small>Este preluată din profilul tău. Pentru prima versiune, potrivirea folosește localitatea și județul.</small>
          @if (form.controls.supplierSearchRadiusKm.touched && form.controls.supplierSearchRadiusKm.invalid) { <small class="field-error">Raza trebuie să fie între 1 și 1.000 km.</small> }</label>
        <div class="two"><label>Data preferată pentru livrare<input type="date" formControlName="preferredDeliveryDate"></label>
          <label>Primesc oferte până la *<input type="datetime-local" formControlName="quotationDeadline">
            <small>La publicare, termenul trebuie să fie în viitor.</small>
            @if (form.controls.quotationDeadline.touched && form.controls.quotationDeadline.invalid) {
              <small class="field-error">{{ form.controls.quotationDeadline.hasError('futureDate') ? 'Termenul-limită trebuie să fie în viitor.' : 'Alege termenul-limită pentru oferte.' }}</small>
            }</label></div>
        <label>Buget estimat (lei)<input type="number" min="0" step="0.01" formControlName="estimatedBudget" placeholder="Opțional">
          @if (form.controls.estimatedBudget.touched && form.controls.estimatedBudget.invalid) { <small class="field-error">Bugetul nu poate fi negativ.</small> }</label>
        <label class="check"><input type="checkbox" formControlName="alternativeProductsAccepted">Accept alternative pentru cerere</label>
        <div class="supplier-notes-field">
          <div class="field-heading">
            <label for="buyer-notes">Observații pentru furnizori <span class="optional">Opțional</span></label>
            <p>Adaugă doar informații care îi ajută să pregătească oferta: acces, preferințe sau constrângeri relevante.</p>
          </div>
          <textarea id="buyer-notes" formControlName="buyerNotes" rows="4" maxlength="1000"
            aria-describedby="buyer-notes-example buyer-notes-count"
            placeholder="Ex.: acces după ora 16:00; te rog să indici separat costul transportului."></textarea>
          <div class="field-meta"><small id="buyer-notes-example">Poți lăsa câmpul gol dacă detaliile sunt deja în descriere sau în pozițiile cererii.</small>
            <small id="buyer-notes-count">{{ form.controls.buyerNotes.value?.length ?? 0 }}/1000</small></div>
        </div>
      </section>
      @if (validationErrors().length) {
        <div class="validation-summary" role="alert"><strong>Verifică următoarele câmpuri:</strong>
          <ul>@for (error of validationErrors(); track error) { <li>{{ error }}</li> }</ul></div>
      }
      <footer><button class="secondary" type="submit" [disabled]="saving() || categoryLoadFailed()">Salvează ciorna</button>
        <button type="button" (click)="openPreview()" [disabled]="saving() || categoryLoadFailed()">Verifică înainte de trimitere →</button></footer>
      @if (previewing()) {
        <section class="request-preview" role="dialog" aria-modal="true" aria-labelledby="preview-title">
          <div class="preview-card">
            <header><span class="preview-check" aria-hidden="true">✓</span><div><p class="eyebrow">Verificare finală</p><h2 id="preview-title">Așa vor vedea furnizorii cererea</h2><p>Recitește informațiile esențiale. Publicarea are loc numai după confirmarea ta.</p></div></header>
            <section class="preview-need"><span class="preview-label">Cerere</span><h3>{{ form.controls.title.value }}</h3>
              @if(form.controls.description.value){<p>{{ form.controls.description.value }}</p>}</section>
            <dl><div><dt>Categorii</dt><dd>{{ selectedCategories().length }} selectate</dd></div><div><dt>Livrare</dt><dd>{{ form.controls.deliveryLocality.value }}, {{ form.controls.county.value }}</dd></div><div><dt>Necesar</dt><dd>{{ items.length ? items.length + ' poziții' : 'Cerere generală' }}</dd></div><div><dt>Oferte până la</dt><dd>{{ form.controls.quotationDeadline.value }}</dd></div></dl>
            @if(items.length){<div class="preview-lines"><h3>Produse sau servicii</h3><ul>@for (item of items.controls; track $index) { <li><span>{{ $index + 1 }}</span><strong>{{ item.controls['productName'].value }}</strong><small>{{ item.controls['quantity'].value }} {{ unitLabel(item.controls['unit'].value) }}</small></li> }</ul></div>}
            <div class="publish-note"><span aria-hidden="true">i</span><p><strong>Ce se întâmplă după publicare?</strong> Furnizorii potriviți pot vedea cererea și îți pot trimite oferte. Vei primi o notificare pentru fiecare ofertă nouă.</p></div>
            <div class="preview-actions"><button type="button" class="secondary" (click)="previewing.set(false)">Înapoi la modificări</button><button type="button" (click)="save(true)" [disabled]="saving()">{{ saving() ? 'Se publică…' : 'Confirmă și publică' }}</button></div>
          </div>
        </section>
      }
    </form>
  </main>`,
  styleUrl: './rfq-editor.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RfqEditorComponent {
  private source: ProcurementNeedSource = 'Manual';
  private inputType: ProcurementInputType = 'StructuredForm';
  @ViewChildren('productNameInput') private productNameInputs!: QueryList<ElementRef<HTMLInputElement>>;
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly service = inject(RfqService);
  private readonly assistedService = inject(AssistedIntakeService);
  private readonly categoriesService = inject(SupplierSettingsService);
  private readonly profiles = inject(ProfileService);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly counties = ROMANIAN_COUNTIES;
  readonly units: { value: UnitOfMeasure; label: string }[] = [
    {value:'Piece',label:'Bucată'},{value:'Pack',label:'Pachet'},{value:'Bag',label:'Sac'},
    {value:'Kilogram',label:'Kilogram'},{value:'Tonne',label:'Tonă'},{value:'Metre',label:'Metru'},
    {value:'SquareMetre',label:'Metru pătrat'},{value:'CubicMetre',label:'Metru cub'},
    {value:'Litre',label:'Litru'},{value:'Pallet',label:'Palet'},{value:'Other',label:'Altă unitate'}];
  readonly categories = signal<CategoryOption[]>([]); readonly categoryLoadFailed = signal(false);
  readonly projectTemplates = signal<ProjectTemplateOption[]>([]);
  readonly constructionEntryMode = signal(this.route.snapshot.queryParamMap.get('entryMode') === 'construction');
  readonly selectedCategoryIds = signal<string[]>([]);
  readonly loading = signal(false); readonly saving = signal(false);
  readonly message = signal(''); readonly hasError = signal(false);
  readonly importMessage = signal(''); readonly importError = signal(false);
  readonly itemMessage = signal('');
  readonly expandedItemIndex = signal<number | null>(null);
  readonly validationErrors = signal<string[]>([]);
  readonly previewing = signal(false);
  readonly pastedLines = new FormControl('', { nonNullable: true });
  readonly rfqId = this.route.snapshot.paramMap.get('rfqId');
  readonly assistedSessionId = this.route.snapshot.queryParamMap.get('assistedSession');
  readonly form = new FormGroup({
    title: new FormControl('', {nonNullable:true,validators:[Validators.required,Validators.maxLength(180)]}),
    projectName:new FormControl<string|null>(null),description:new FormControl<string|null>(null),
    primaryCategoryId:new FormControl('',{nonNullable:true,validators:Validators.required}),
    needType:new FormControl<ProcurementNeedType>('DirectPurchase',{nonNullable:true}),
    projectTemplateId:new FormControl<string|null>(null),
    deliveryAddress:new FormControl('',{nonNullable:true,validators:Validators.required}),
    deliveryLocality:new FormControl('',{nonNullable:true,validators:Validators.required}),
    county:new FormControl('',{nonNullable:true,validators:Validators.required}),
    supplierSearchRadiusKm:new FormControl(30,{nonNullable:true,validators:[Validators.required,Validators.min(1),Validators.max(1000)]}),
    preferredDeliveryDate:new FormControl<string|null>(null),
    quotationDeadline:new FormControl(this.defaultDeadline(),{nonNullable:true,validators:[
      Validators.required,
      control=>!control.value||new Date(control.value).getTime()>Date.now()?null:{futureDate:true}
    ]}),
    alternativeProductsAccepted:new FormControl(true,{nonNullable:true}),
    allowSplit:new FormControl(false,{nonNullable:true}),
    deliveryRequired:new FormControl(true,{nonNullable:true}),
    estimatedBudget:new FormControl<number|null>(null,{validators:Validators.min(0)}),
    buyerNotes:new FormControl<string|null>(null),
    items:new FormArray<FormGroup>([])
  });
  get items(): FormArray<FormGroup> { return this.form.controls.items; }
  selectableCategories(): CategoryOption[] {
    const values=this.categories(); const parents=new Set(values.flatMap(value=>value.parentId?[value.parentId]:[]));
    // Scope by entry mode so Construction (depth) and General (breadth) do not leak each other's categories.
    // Already-selected categories always stay visible so editing an existing request never drops a selection.
    const activeVertical=this.constructionEntryMode()?CONSTRUCTION_VERTICAL_ID:GENERAL_PROCUREMENT_VERTICAL_ID;
    const selected=new Set(this.selectedCategoryIds());
    return values.filter(value=>!parents.has(value.id)&&(value.verticalId===activeVertical||selected.has(value.id)));
  }

  constructor() {
    const navigationMessage = history.state?.message;
    const initialDescription = this.route.snapshot.queryParamMap.get('description')?.trim() ?? '';
    if (typeof navigationMessage === 'string') this.message.set(navigationMessage);
    this.loadCategories();
    if (this.constructionEntryMode()) {
      this.form.controls.needType.setValue('Project');
      this.loadProjectTemplates();
    }
    if (this.rfqId) {
      this.loading.set(true);
      this.service.get(this.rfqId).subscribe({
        next: value => { this.patch(value); this.loading.set(false); },
        error: error => { this.loading.set(false); this.fail(error.status===404?'Cererea nu există sau nu îți aparține.':'Cererea nu a putut fi încărcată.'); }
      });
    } else if (this.assistedSessionId) {
      this.loading.set(true);
      this.assistedService.get(this.assistedSessionId).subscribe({
        next:value=>{
          if(value.approvedRfqId){void this.router.navigate(['/rfqs',value.approvedRfqId,'edit'],{replaceUrl:true});return;}
          this.patchAssisted(value);this.loading.set(false);this.clearNavigationMessage();
        },
        error:()=>{this.loading.set(false);this.fail('Draftul asistat nu a putut fi încărcat. Revino la cererea asistată și încearcă din nou.');}
      });
    } else {
      if (initialDescription) this.form.patchValue({ description: initialDescription, title: initialDescription.slice(0, 180) });
      const identity=this.auth.identity();
      if(identity) this.profiles.getBuyer(identity.userId).subscribe({
        next:p=>this.form.patchValue({deliveryLocality:p.defaultCity,county:p.defaultCounty,supplierSearchRadiusKm:p.marketRadiusKm}),
        error:error=>{
          if(error.status!==404) {
            this.fail('Profilul de cumpărător nu a putut fi verificat. Reîncarcă pagina și încearcă din nou.');
          }
        }
      });
    }
  }

  selectedCategories():CategoryOption[]{
    const ids=new Set(this.selectedCategoryIds());
    return this.selectableCategories().filter(category=>ids.has(category.id));
  }

  loadCategories(): void {
    this.categoryLoadFailed.set(false);
    this.categoriesService.getCategories().subscribe({
      next: values => this.categories.set(values),
      error: () => { this.categoryLoadFailed.set(true); this.fail('Categoriile nu au putut fi încărcate.'); }
    });
  }
  private loadProjectTemplates(): void {
    this.categoriesService.getProjectTemplates().subscribe({
      next: values => this.projectTemplates.set(values),
      error: () => this.projectTemplates.set([])
    });
  }
  toggleNeedCategory(id:string):void{
    const current=this.selectedCategoryIds();
    if(current.includes(id)){
      if(this.items.controls.some(item=>item.controls['categoryId'].value===id)){
        this.fail('Categoria este folosită de o poziție. Mută poziția într-o altă categorie înainte să o elimini.');
        return;
      }
      const next=current.filter(value=>value!==id);
      this.selectedCategoryIds.set(next);this.form.controls.primaryCategoryId.setValue(next[0]??'');
    }else{
      const next=[...current,id];
      this.selectedCategoryIds.set(next);this.form.controls.primaryCategoryId.setValue(next[0]);
    }
  }
  applyProjectTemplate():void{
    const template=this.projectTemplates().find(value=>value.id===this.form.controls.projectTemplateId.value);
    if(!template)return;
    const categoriesAlreadyUsedByLines=this.items.controls
      .map(item=>item.controls['categoryId'].value)
      .filter((id):id is string=>!!id);
    const proposedCategoryIds=Array.from(new Set([...template.categoryIds,...categoriesAlreadyUsedByLines]));
    this.selectedCategoryIds.set(proposedCategoryIds);
    this.form.controls.primaryCategoryId.setValue(proposedCategoryIds[0]??'');
  }

  addItem(value?: Partial<Rfq['items'][number]>, activate = true): void {
    const current = this.items.at(this.items.length - 1);
    if (!value && current?.invalid) {
      current.markAllAsTouched();
      this.expandedItemIndex.set(this.items.length - 1);
      this.itemMessage.set('Completează denumirea și cantitatea poziției curente înainte de a adăuga alta.');
      this.focusProductName(this.items.length - 1);
      return;
    }

    this.items.push(new FormGroup({
      productName:new FormControl(value?.productName??'',{nonNullable:true,validators:Validators.required}),
      kind:new FormControl<RequirementKind>(value?.kind??'Product',{nonNullable:true}),
      manufacturerOrBrand:new FormControl(value?.manufacturerOrBrand??null),
      specification:new FormControl(value?.specification??null),
      quantity:new FormControl(value?.quantity??1,{nonNullable:true,validators:[Validators.required,Validators.min(.01)]}),
      unit:new FormControl<UnitOfMeasure>(value?.unit??'Piece',{nonNullable:true}),
      alternativesAccepted:new FormControl(value?.alternativesAccepted??true,{nonNullable:true}),
      buyerNotes:new FormControl(value?.buyerNotes??null),
      categoryId:new FormControl<string|null>((value?.categoryId??this.selectedCategoryIds()[0])||null,{validators:Validators.required})
    }));
    this.itemMessage.set('');
    if (activate) {
      const newIndex = this.items.length - 1;
      this.expandedItemIndex.set(newIndex);
      this.focusProductName(newIndex);
    }
  }
  onItemToggle(index:number,event:Event):void {
    const details=event.currentTarget as HTMLDetailsElement;
    if(details.open) this.expandedItemIndex.set(index);
    else if(this.expandedItemIndex()===index) this.expandedItemIndex.set(null);
  }
  removeItem(index:number):void {
    const expanded=this.expandedItemIndex();
    this.items.removeAt(index);
    this.itemMessage.set('');
    if(expanded===index) this.expandedItemIndex.set(null);
    else if(expanded!==null&&expanded>index) this.expandedItemIndex.set(expanded-1);
  }
  importLines():void {
    const lines=this.pastedLines.value.split(/\r?\n/).map(line=>line.trim()).filter(Boolean);
    if(lines.length>0&&this.items.length===1&&!String(this.items.controls[0].controls['productName'].value).trim()) this.items.clear();
    let added=0; const invalid:number[]=[];
    lines.forEach((line,index)=>{
      const parts=line.split(line.includes('\t')?'\t':';').map(value=>value.trim());
      const quantity=Number(parts[1]?.replace(',','.')); const unit=this.parseUnit(parts[2]??'');
      if(!parts[0]||!Number.isFinite(quantity)||quantity<=0||!unit){invalid.push(index+1);return;}
      this.addItem({productName:parts[0],quantity,unit}); added++;
    });
    this.importError.set(invalid.length>0);
    this.importMessage.set(invalid.length?`Au fost adăugate ${added} rânduri. Verifică rândurile ${invalid.join(', ')}: cantitatea sau unitatea nu este validă.`:`Au fost adăugate ${added} poziții.`);
    if(added) this.pastedLines.reset();
  }
  openPreview(): void {
    this.message.set(''); this.hasError.set(false); this.validationErrors.set([]);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.validationErrors.set(this.collectValidationErrors());
      this.hasError.set(true);
      const invalidItemIndex=this.items.controls.findIndex(item=>item.invalid);
      if(invalidItemIndex>=0) this.expandedItemIndex.set(invalidItemIndex);
      this.scrollToFirstInvalidField();
      return;
    }
    this.previewing.set(true);
    setTimeout(()=>{
      const preview=this.host.nativeElement.querySelector('.request-preview') as HTMLElement | null;
      if (typeof preview?.scrollIntoView === 'function') preview.scrollIntoView({behavior:'smooth',block:'center'});
    });
  }
  save(publish:boolean):void {
    this.message.set(''); this.hasError.set(false); this.validationErrors.set([]);
    if(this.form.invalid){
      this.form.markAllAsTouched();
      this.validationErrors.set(this.collectValidationErrors());
      this.hasError.set(true);
      const invalidItemIndex=this.items.controls.findIndex(item=>item.invalid);
      if(invalidItemIndex>=0) this.expandedItemIndex.set(invalidItemIndex);
      this.scrollToFirstInvalidField();
      return;
    }
    this.previewing.set(false); this.saving.set(true); const draft=this.toDraft();
    const saveRequest=this.rfqId?this.service.update(this.rfqId,draft):this.assistedSessionId?this.assistedService.approve(this.assistedSessionId,draft,true):this.service.create(draft);
    saveRequest.pipe(switchMap(saved=>publish?this.service.publish(saved.id):[saved])).subscribe({
      next: saved => {this.saving.set(false);void this.router.navigateByUrl('/dashboard',{state:{message:saved.status==='Published'?'Cererea a fost publicată.':'Ciorna a fost salvată.'}});},
      error: error => {this.saving.set(false);this.fail(this.apiError(error));}
    });
  }
  private patch(rfq:Rfq):void {
    this.source=rfq.source;this.inputType=rfq.inputType;
    if(rfq.projectTemplateId){this.constructionEntryMode.set(true);this.loadProjectTemplates();}
    this.form.patchValue({...rfq,quotationDeadline:this.localDateTime(rfq.quotationDeadline)});
    this.selectedCategoryIds.set(rfq.categoryIds?.length?rfq.categoryIds:[rfq.primaryCategoryId]);
    this.items.clear();rfq.items.forEach(item=>this.addItem(item,false));
    this.expandedItemIndex.set(null);
  }
  private patchAssisted(result:AssistedIntakeResult):void {
    const draft=result.draft;
    this.source='Assisted';this.inputType=draft.inputType;
    this.form.patchValue({
      title:draft.title,projectName:draft.projectName,description:draft.description,
      primaryCategoryId:draft.primaryCategoryId??'',deliveryAddress:draft.deliveryAddress,
      deliveryLocality:draft.deliveryLocality,county:this.canonicalCounty(draft.county),
      supplierSearchRadiusKm:draft.supplierSearchRadiusKm,preferredDeliveryDate:draft.preferredDeliveryDate,
      quotationDeadline:this.localDateTime(draft.quotationDeadline),alternativeProductsAccepted:draft.alternativeProductsAccepted,
      allowSplit:draft.allowSplit,deliveryRequired:draft.deliveryRequired,estimatedBudget:draft.estimatedBudget,
      buyerNotes:this.assistedNotes(draft.buyerNotes,result)
    });
    const assistedCategoryIds=Array.from(new Set([draft.primaryCategoryId,...draft.lines.map(line=>line.categoryId)].filter((id):id is string=>!!id)));
    this.selectedCategoryIds.set(assistedCategoryIds);
    this.items.clear();
    draft.lines.forEach(line=>this.addItem({productName:line.name,kind:line.kind,manufacturerOrBrand:null,
      specification:line.specification,quantity:line.quantity??0,unit:line.unit??'Other',
      alternativesAccepted:line.alternativesAccepted,buyerNotes:null,categoryId:line.categoryId},false));
    this.expandedItemIndex.set(null);
  }
  private clearNavigationMessage():void {
    this.message.set('');this.hasError.set(false);
    const state={...history.state};delete state.message;history.replaceState(state,'');
  }
  private assistedNotes(existing:string|null,result:AssistedIntakeResult):string|null {
    if(!result.answers.length)return existing;
    const heading='Clarificări din asistent:';
    if(existing?.includes(heading))return existing;
    const transcript=result.answers.map(entry=>`Î: ${entry.question}\nR: ${entry.answer}`).join('\n');
    return [existing?.trim(),heading,transcript].filter(Boolean).join('\n\n').slice(0,2000);
  }
  private canonicalCounty(value:string):string {
    const fold=(text:string)=>text.normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLocaleLowerCase('ro-RO');
    return this.counties.find(county=>fold(county)===fold(value))??value;
  }
  private toDraft():RfqDraft {
    const value=this.form.getRawValue();
    return {...value,categoryIds:this.selectedCategoryIds(),projectTemplateId:this.constructionEntryMode()?value.projectTemplateId:null,
      source:this.source,inputType:this.inputType,latitude:null,longitude:null,quotationDeadline:new Date(value.quotationDeadline).toISOString(),items:value.items} as RfqDraft;
  }
  private parseUnit(value:string):UnitOfMeasure|null {
    const key=value.toLocaleLowerCase('ro-RO').replace(/[ăâ]/g,'a').replace(/ț/g,'t').replace(/ș/g,'s').trim();
    const aliases:Record<string,UnitOfMeasure>={'bucata':'Piece','buc':'Piece','piece':'Piece','pachet':'Pack','pack':'Pack','sac':'Bag','bag':'Bag','kg':'Kilogram','kilogram':'Kilogram','tona':'Tonne','t':'Tonne','metru':'Metre','m':'Metre','mp':'SquareMetre','m2':'SquareMetre','mc':'CubicMetre','m3':'CubicMetre','litru':'Litre','l':'Litre','palet':'Pallet','alta':'Other','other':'Other'};
    return aliases[key]??null;
  }
  unitLabel(value: unknown): string { return this.units.find(unit => unit.value === value)?.label ?? 'unități'; }
  kindLabel(value: unknown): string { return value === 'Service' ? 'Serviciu' : 'Produs'; }
  categoryName(value: unknown): string {
    if (!value) return 'Categorie nespecificată';
    return this.categories().find(category => category.id === value)?.name ?? 'Categorie';
  }
  private apiError(error:HttpErrorResponse):string {
    if(error.status===0)return 'API-ul nu poate fi contactat. Pornește Marketplace.Api și încearcă din nou.';
    const messages=error.error?.errors?Object.values(error.error.errors as Record<string,string[]>).flat():[];
    if(messages.length)return messages.join(' ');
    if(error.error?.detail)return error.error.detail;
    if(error.status===401)return 'Sesiunea a expirat. Autentifică-te din nou.';
    if(error.status===403)return 'Comută pe profilul de cumpărător pentru a salva o cerere.';
    return `Cererea nu a putut fi salvată (HTTP ${error.status}).`;
  }
  private collectValidationErrors():string[] {
    const errors:string[]=[];
    if(this.form.controls.title.invalid) errors.push('Completează titlul cererii.');
    if(this.form.controls.primaryCategoryId.invalid||!this.selectedCategoryIds().length) errors.push('Selectează cel puțin o categorie pentru necesar.');
    this.items.controls.forEach((item,index)=>{
      if(item.controls['productName'].invalid) errors.push(`Poziția ${index+1}: completează denumirea.`);
      if(item.controls['quantity'].invalid) errors.push(`Poziția ${index+1}: cantitatea trebuie să fie mai mare decât 0.`);
      if(item.controls['categoryId'].invalid) errors.push(`Poziția ${index+1}: selectează categoria relevantă.`);
    });
    if(this.form.controls.deliveryAddress.invalid) errors.push('Completează adresa de livrare.');
    if(this.form.controls.deliveryLocality.invalid) errors.push('Completează localitatea.');
    if(this.form.controls.county.invalid) errors.push('Selectează județul.');
    if(this.form.controls.supplierSearchRadiusKm.invalid) errors.push('Raza de căutare trebuie să fie între 1 și 1.000 km.');
    if(this.form.controls.quotationDeadline.hasError('required')) errors.push('Alege termenul-limită pentru oferte.');
    else if(this.form.controls.quotationDeadline.hasError('futureDate')) errors.push('Termenul-limită pentru oferte trebuie să fie în viitor.');
    if(this.form.controls.estimatedBudget.invalid) errors.push('Bugetul nu poate fi negativ.');
    return errors;
  }
  private scrollToFirstInvalidField():void {
    setTimeout(()=>{
      const field=(this.host.nativeElement as HTMLElement).querySelector<HTMLElement>('input.ng-invalid.ng-touched, select.ng-invalid.ng-touched, textarea.ng-invalid.ng-touched');
      if(typeof field?.scrollIntoView==='function') field.scrollIntoView({behavior:'smooth',block:'center'});
      field?.focus();
    });
  }
  private fail(message:string):void {
    this.validationErrors.set([]);
    this.hasError.set(true);
    this.message.set(message);
    setTimeout(()=>{
      const alert=(this.host.nativeElement as HTMLElement).querySelector<HTMLElement>('.message.error');
      if(typeof alert?.scrollIntoView==='function') alert.scrollIntoView({behavior:'smooth',block:'center'});
    });
  }
  private focusProductName(index:number):void {
    setTimeout(()=>this.productNameInputs.get(index)?.nativeElement.focus());
  }
  private defaultDeadline():string {const d=new Date(Date.now()+24*60*60*1000);d.setMinutes(0,0,0);return this.localDateTime(d.toISOString());}
  private localDateTime(value:string):string {const d=new Date(value);const offset=d.getTimezoneOffset()*60000;return new Date(d.getTime()-offset).toISOString().slice(0,16);}
}
