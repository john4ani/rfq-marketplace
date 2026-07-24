import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { switchMap } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { ProfileService } from '../../core/profiles/profile.service';
import { Rfq, RfqDraft, RfqService, UnitOfMeasure } from '../../core/rfqs/rfq.service';
import { CategoryOption, SupplierSettingsService } from '../../core/suppliers/supplier-settings.service';
import { ROMANIAN_COUNTIES } from '../../shared/romanian-counties';

@Component({
  selector: 'app-rfq-editor',
  imports: [ReactiveFormsModule, RouterLink],
  template: `<main class="editor-shell">
    <a class="back" routerLink="/dashboard">← Înapoi la cont</a>
    <header><p class="eyebrow">Cerere de ofertă</p><h1>{{ rfqId ? 'Editează ciorna' : 'Cerere nouă' }}</h1>
      <p>Salvează acum și publică doar când lista este pregătită.</p></header>
    @if (loading()) { <p role="status">Se încarcă cererea…</p> }
    <form [formGroup]="form" (ngSubmit)="save(false)">
      <section><h2>1. Despre necesar</h2>
        <label>Titlu clar *<input formControlName="title" maxlength="180" placeholder="Exemplu: Materiale pentru renovare apartament">
          <small>Spune pe scurt furnizorilor ce dorești să cumperi.</small></label>
        <div class="two"><label>Proiect<input formControlName="projectName" placeholder="Opțional"></label>
          <label>Categorie principală *<select formControlName="primaryCategoryId"><option value="">Alege categoria</option>
            @for (category of selectableCategories(); track category.id) { <option [value]="category.id">{{ category.name }}</option> }</select></label></div>
        <label>Descriere<textarea formControlName="description" rows="3" placeholder="Contextul proiectului sau cerințe generale"></textarea></label>
      </section>

      <section><div class="section-title"><div><h2>2. Materiale individuale necesare <span class="optional">Opțional</span></h2>
        <p>Adaugă produse și cantități doar dacă ai deja o listă exactă.</p></div>
        <button class="secondary" type="button" (click)="addItem()">{{ items.length === 0 ? '+ Adaugă materiale individuale' : '+ Adaugă rând' }}</button></div>
        @if (items.length === 0) {
          <div class="optional-placeholder"><strong>Poți continua fără materiale individuale.</strong>
            <span>Descrierea și categoria sunt suficiente pentru o cerere generală.</span></div>
        } @else {
          <details><summary>Lipește mai multe materiale</summary>
            <p class="help">Câte un material pe rând: denumire, cantitate și unitate, separate prin Tab sau „;”. Exemplu: <code>Ciment;20;Sac</code></p>
            <textarea [formControl]="pastedLines" rows="5" placeholder="Ciment 40 kg&#9;20&#9;Sac&#10;Plasă sudată&#9;12&#9;Bucată"></textarea>
            <button class="secondary" type="button" (click)="importLines()">Adaugă lista</button>
            @if (importMessage()) { <p [class.error]="importError()">{{ importMessage() }}</p> }
          </details>
          <div formArrayName="items" class="items">
            @for (item of items.controls; track $index; let index = $index) {
              <div class="item" [formGroupName]="index"><strong>Material {{ index + 1 }}</strong>
                <button class="remove" type="button" (click)="removeItem(index)" aria-label="Șterge materialul">Șterge</button>
                <label>Denumire *<input formControlName="productName" placeholder="Exemplu: Ciment 40 kg"></label>
                <div class="three"><label>Cantitate *<input type="number" min="0.01" step="0.01" formControlName="quantity"></label>
                  <label>Unitate *<select formControlName="unit">@for (unit of units; track unit.value) { <option [value]="unit.value">{{ unit.label }}</option> }</select></label>
                  <label>Marcă / producător<input formControlName="manufacturerOrBrand"></label></div>
                <label>Specificații<input formControlName="specification" placeholder="Dimensiuni, clasă, culoare…"></label>
                <label class="check"><input type="checkbox" formControlName="alternativesAccepted">Accept produse echivalente pentru acest material</label>
              </div>
            }
          </div>
        }
      </section>

      <section><h2>3. Livrare și termen</h2>
        <label>Adresa de livrare *<input formControlName="deliveryAddress" placeholder="Stradă, număr și detalii de acces"></label>
        <div class="two"><label>Localitate *<input formControlName="deliveryLocality"></label>
          <label>Județ *<select formControlName="county"><option value="">Alege județul</option>@for (county of counties; track county) { <option [value]="county">{{ county }}</option> }</select></label></div>
        <label>Rază de căutare a furnizorilor (km) *<input type="number" min="1" max="1000" formControlName="supplierSearchRadiusKm">
          <small>Este preluată din profilul tău. Pentru prima versiune, potrivirea folosește localitatea și județul.</small></label>
        <div class="two"><label>Data preferată pentru livrare<input type="date" formControlName="preferredDeliveryDate"></label>
          <label>Primesc oferte până la *<input type="datetime-local" formControlName="quotationDeadline">
            <small>La publicare, termenul trebuie să fie în viitor.</small></label></div>
        <label>Buget estimat (lei)<input type="number" min="0" step="0.01" formControlName="estimatedBudget" placeholder="Opțional"></label>
        <label class="check"><input type="checkbox" formControlName="alternativeProductsAccepted">Accept alternative pentru cerere</label>
        <label>Observații pentru furnizori<textarea formControlName="buyerNotes" rows="3"></textarea></label>
      </section>
      @if (message()) { <p class="message" [class.error]="hasError()" role="alert">{{ message() }}</p> }
      <footer><button class="secondary" type="submit" [disabled]="saving()">Salvează ciorna</button>
        <button type="button" (click)="save(true)" [disabled]="saving()">{{ saving() ? 'Se salvează…' : 'Salvează și publică' }}</button></footer>
    </form>
  </main>`,
  styleUrl: './rfq-editor.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RfqEditorComponent {
  private readonly service = inject(RfqService);
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
  readonly categories = signal<CategoryOption[]>([]);
  readonly loading = signal(false); readonly saving = signal(false);
  readonly message = signal(''); readonly hasError = signal(false);
  readonly importMessage = signal(''); readonly importError = signal(false);
  readonly pastedLines = new FormControl('', { nonNullable: true });
  readonly rfqId = this.route.snapshot.paramMap.get('rfqId');
  readonly form = new FormGroup({
    title: new FormControl('', {nonNullable:true,validators:[Validators.required,Validators.maxLength(180)]}),
    projectName:new FormControl<string|null>(null),description:new FormControl<string|null>(null),
    primaryCategoryId:new FormControl('',{nonNullable:true,validators:Validators.required}),
    deliveryAddress:new FormControl('',{nonNullable:true,validators:Validators.required}),
    deliveryLocality:new FormControl('',{nonNullable:true,validators:Validators.required}),
    county:new FormControl('',{nonNullable:true,validators:Validators.required}),
    supplierSearchRadiusKm:new FormControl(30,{nonNullable:true,validators:[Validators.required,Validators.min(1),Validators.max(1000)]}),
    preferredDeliveryDate:new FormControl<string|null>(null),
    quotationDeadline:new FormControl(this.defaultDeadline(),{nonNullable:true,validators:Validators.required}),
    alternativeProductsAccepted:new FormControl(true,{nonNullable:true}),
    estimatedBudget:new FormControl<number|null>(null,{validators:Validators.min(0)}),
    buyerNotes:new FormControl<string|null>(null),
    items:new FormArray<FormGroup>([])
  });
  get items(): FormArray<FormGroup> { return this.form.controls.items; }
  selectableCategories(): CategoryOption[] {
    const values=this.categories(); const parents=new Set(values.flatMap(value=>value.parentId?[value.parentId]:[]));
    return values.filter(value=>!parents.has(value.id));
  }

  constructor() {
    if (this.auth.session()?.accountType !== 'Buyer') { void this.router.navigateByUrl('/dashboard'); return; }
    this.categoriesService.getCategories().subscribe({
      next: values => this.categories.set(values),
      error: () => this.fail('Categoriile nu au putut fi încărcate. Verifică dacă API-ul rulează.')
    });
    if (this.rfqId) {
      this.loading.set(true);
      this.service.get(this.rfqId).subscribe({
        next: value => { this.patch(value); this.loading.set(false); },
        error: error => { this.loading.set(false); this.fail(error.status===404?'Cererea nu există sau nu îți aparține.':'Cererea nu a putut fi încărcată.'); }
      });
    } else {
      const session=this.auth.session();
      if(session) this.profiles.getBuyer(session.userId).subscribe({next:p=>this.form.patchValue({deliveryLocality:p.defaultCity,county:p.defaultCounty,supplierSearchRadiusKm:p.marketRadiusKm})});
    }
  }

  addItem(value?: Partial<Rfq['items'][number]>): void {
    this.items.push(new FormGroup({
      productName:new FormControl(value?.productName??'',{nonNullable:true,validators:Validators.required}),
      manufacturerOrBrand:new FormControl(value?.manufacturerOrBrand??null),
      specification:new FormControl(value?.specification??null),
      quantity:new FormControl(value?.quantity??1,{nonNullable:true,validators:[Validators.required,Validators.min(.01)]}),
      unit:new FormControl<UnitOfMeasure>(value?.unit??'Piece',{nonNullable:true}),
      alternativesAccepted:new FormControl(value?.alternativesAccepted??true,{nonNullable:true}),
      buyerNotes:new FormControl(value?.buyerNotes??null)
    }));
  }
  removeItem(index:number):void { this.items.removeAt(index); }
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
    this.importMessage.set(invalid.length?`Au fost adăugate ${added} rânduri. Verifică rândurile ${invalid.join(', ')}: cantitatea sau unitatea nu este validă.`:`Au fost adăugate ${added} materiale.`);
    if(added) this.pastedLines.reset();
  }
  save(publish:boolean):void {
    this.message.set(''); this.hasError.set(false);
    if(this.form.invalid){this.form.markAllAsTouched();this.fail('Completează câmpurile obligatorii și verifică valorile introduse.');return;}
    this.saving.set(true); const draft=this.toDraft();
    const saveRequest=this.rfqId?this.service.update(this.rfqId,draft):this.service.create(draft);
    saveRequest.pipe(switchMap(saved=>publish?this.service.publish(saved.id):[saved])).subscribe({
      next: saved => {this.saving.set(false);void this.router.navigateByUrl('/dashboard',{state:{message:saved.status==='Published'?'Cererea a fost publicată.':'Ciorna a fost salvată.'}});},
      error: error => {this.saving.set(false);this.fail(this.apiError(error));}
    });
  }
  private patch(rfq:Rfq):void {
    this.form.patchValue({...rfq,quotationDeadline:this.localDateTime(rfq.quotationDeadline)});
    this.items.clear();rfq.items.forEach(item=>this.addItem(item));
  }
  private toDraft():RfqDraft {
    const value=this.form.getRawValue();
    return {...value,latitude:null,longitude:null,quotationDeadline:new Date(value.quotationDeadline).toISOString(),items:value.items} as RfqDraft;
  }
  private parseUnit(value:string):UnitOfMeasure|null {
    const key=value.toLocaleLowerCase('ro-RO').replace(/[ăâ]/g,'a').replace(/ț/g,'t').replace(/ș/g,'s').trim();
    const aliases:Record<string,UnitOfMeasure>={'bucata':'Piece','buc':'Piece','piece':'Piece','pachet':'Pack','pack':'Pack','sac':'Bag','bag':'Bag','kg':'Kilogram','kilogram':'Kilogram','tona':'Tonne','t':'Tonne','metru':'Metre','m':'Metre','mp':'SquareMetre','m2':'SquareMetre','mc':'CubicMetre','m3':'CubicMetre','litru':'Litre','l':'Litre','palet':'Pallet','alta':'Other','other':'Other'};
    return aliases[key]??null;
  }
  private apiError(error:HttpErrorResponse):string {
    if(error.status===0)return 'API-ul nu poate fi contactat. Pornește Marketplace.Api și încearcă din nou.';
    const messages=error.error?.errors?Object.values(error.error.errors as Record<string,string[]>).flat():[];
    if(messages.length)return messages.join(' ');
    if(error.status===401)return 'Sesiunea a expirat. Autentifică-te din nou.';
    if(error.status===403)return 'Comută pe profilul de cumpărător pentru a salva o cerere.';
    return `Cererea nu a putut fi salvată (HTTP ${error.status}).`;
  }
  private fail(message:string):void {this.hasError.set(true);this.message.set(message);}
  private defaultDeadline():string {const d=new Date(Date.now()+24*60*60*1000);d.setMinutes(0,0,0);return this.localDateTime(d.toISOString());}
  private localDateTime(value:string):string {const d=new Date(value);const offset=d.getTimezoneOffset()*60000;return new Date(d.getTime()-offset).toISOString().slice(0,16);}
}
