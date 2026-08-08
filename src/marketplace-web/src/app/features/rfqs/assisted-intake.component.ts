import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, ElementRef, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AssistedDraft, AssistedIntakeResult, AssistedIntakeService } from '../../core/rfqs/assisted-intake.service';
import { RfqDraft, UnitOfMeasure } from '../../core/rfqs/rfq.service';
import { CategoryOption, SupplierSettingsService } from '../../core/suppliers/supplier-settings.service';
import { ROMANIAN_COUNTIES } from '../../shared/romanian-counties';
import { unitLabel } from '../../shared/unit-label';

@Component({
  selector: 'app-assisted-intake',
  imports: [FormsModule, RouterLink],
  template: `<main class="intake-shell">
    <a class="back" routerLink="/dashboard">← Înapoi acasă</a>
    <header><p class="eyebrow">Cerere nouă</p><h1>Spune-ne ce ai nevoie</h1>
      <p>Scrie natural, ca într-un mesaj. Asistentul îți pune doar întrebările care mai contează.</p></header>
    @if (checkingAvailability()) {
      <section class="start-card" aria-live="polite">Se pregătește asistentul…</section>
    } @else if (!result()) {
      <section class="start-card"><label for="need">Ce dorești să cumperi sau să construiești?</label>
        <textarea id="need" [(ngModel)]="input" rows="7" maxlength="4000" placeholder="Exemplu: Am nevoie de 2 tone de peleți, livrare în Brașov, la sac..."></textarea>
        <div class="starter-note"><span aria-hidden="true">✦</span> Te ajutăm să structurăm cererea. Nu trimitem nimic fără acordul tău.</div>
        <div class="actions"><a routerLink="/rfqs/new/advanced">Am deja lista — deschide formularul</a>
          <button type="button" (click)="start()" [disabled]="busy() || input.trim().length < 10">{{ busy() ? 'Analizăm ce ai nevoie…' : 'Continuă cu AI →' }}</button></div>
      </section>
    } @else {
      <div class="workspace">
        <section class="conversation">
          <div class="user-message"><span>Ai descris</span><p>{{ input }}</p></div>
          @for (entry of result()?.answers; track $index) {
            <div class="history"><span>Clarificare {{ $index + 1 }}</span><p class="history-question">{{ entry.question }}</p><p><strong>Răspunsul tău:</strong> {{ entry.answer }}</p></div>
          }
          @if (result()?.clarification; as clarification) {
            <div class="question"><span>O clarificare importantă</span><h2>{{ clarification.question }}</h2>
              @if (clarification.choices.length) { <div class="choices">@for (choice of clarification.choices; track choice) { <button type="button" (click)="answer=choice; clarify()" [disabled]="busy()">{{ choice }}</button> }</div> }
              @else { <div class="answer"><input [(ngModel)]="answer" (keyup.enter)="clarify()" placeholder="Scrie răspunsul"><button type="button" (click)="clarify()" [disabled]="busy() || !answer.trim()">Răspunde</button></div> }
            </div>
          } @else { <div class="ready"><strong>Draft pregătit pentru verificare</strong><p>Nu este publicat automat. Îl poți corecta înainte să-l salvezi.</p></div> }
        </section>
        <aside class="understanding"><h2>Ce am înțeles</h2>
          <label>Titlu *<input [(ngModel)]="draft.title"></label>
          <label>Categorie *<select [(ngModel)]="draft.primaryCategoryId"><option [ngValue]="null">Alege categoria</option>@for(category of leafCategories();track category.id){<option [value]="category.id">{{category.name}}</option>}</select></label>
          <label>Descriere<textarea [(ngModel)]="draft.description" rows="3"></textarea></label>
          <div class="two"><label>Localitate *<input [(ngModel)]="draft.deliveryLocality"></label><label>Județ *<input [(ngModel)]="draft.county"></label></div>
          <label>Adresă de livrare *<input [(ngModel)]="draft.deliveryAddress"></label>
          <h3>Produse sau servicii</h3>
          @for(line of draft.lines;track $index){<div class="line"><select [(ngModel)]="line.kind"><option value="Product">Produs</option><option value="Service">Serviciu</option></select><input class="name" [(ngModel)]="line.name" aria-label="Denumire"><input type="number" min="0.01" [(ngModel)]="line.quantity" aria-label="Cantitate"><select [(ngModel)]="line.unit"><option [ngValue]="null">Unitate</option>@for(unit of units;track unit){<option [value]="unit">{{unitLabel(unit)}}</option>}</select></div>}
          @if(result()?.assumptions?.length){<details open><summary>Presupuneri</summary><ul>@for(item of result()?.assumptions;track item){<li>{{item}}</li>}</ul></details>}
          @if(result()?.uncertainties?.length){<details open class="uncertain"><summary>De verificat</summary><ul>@for(item of result()?.uncertainties;track item){<li>{{item}}</li>}</ul></details>}
          @if(result()?.researchSources?.length){<details open class="sources"><summary>Surse folosite pentru propunere</summary><ul>@for(source of result()?.researchSources;track source.url){<li><a [href]="source.url" target="_blank" rel="noopener noreferrer">{{source.title}}</a><p>{{source.finding}}</p></li>}</ul></details>}
          @if (!result()?.clarification) { <button class="approve" type="button" (click)="approve()" [disabled]="busy()">{{busy()?'Se salvează…':'Aprobă și salvează ciorna'}}</button> }
          @if (!result()?.clarification) {
            <a class="advanced" routerLink="/rfqs/new/advanced" [queryParams]="{assistedSession: result()?.sessionId}">Continuă acest draft în formularul avansat</a>
          } @else {
            <p class="advanced-note">După această clarificare poți continua același draft în formularul avansat.</p>
          }
        </aside>
      </div>
    }
    @if(message()){<p class="message" [class.error]="hasError()" role="alert">{{message()}}
      @if(needsBuyerProfile()){<a class="message-action" routerLink="/dashboard" [queryParams]="{section:'settings'}">Completează profilul</a>}
    </p>}
  </main>`,
  styleUrl: './assisted-intake.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AssistedIntakeComponent {
  private readonly service=inject(AssistedIntakeService); private readonly categoriesService=inject(SupplierSettingsService);
  private readonly route=inject(ActivatedRoute); private readonly router=inject(Router); private readonly host=inject(ElementRef<HTMLElement>);
  readonly result=signal<AssistedIntakeResult|null>(null); readonly categories=signal<CategoryOption[]>([]); readonly busy=signal(false); readonly message=signal(''); readonly hasError=signal(false); readonly needsBuyerProfile=signal(false); readonly checkingAvailability=signal(true);
  input=''; answer=''; draft:AssistedDraft=this.emptyDraft();
  readonly units:UnitOfMeasure[]=['Piece','Pack','Bag','Kilogram','Tonne','Metre','SquareMetre','CubicMetre','Litre','Pallet','Other'];
  readonly unitLabel=unitLabel;
  constructor(){
    const id=this.route.snapshot.queryParamMap.get('session');
    const dashboardDescription=this.route.snapshot.queryParamMap.get('description')?.trim()??'';
    this.input=dashboardDescription;
    this.service.availability().subscribe({next:value=>{
      if(!value.enabled||!value.modelAvailable){this.fallbackToAdvanced(id);return;}
      this.checkingAvailability.set(false);
      this.categoriesService.getCategories().subscribe({next:v=>this.categories.set(v)});
      if(id){this.busy.set(true);this.service.get(id).subscribe({next:v=>this.apply(v),error:e=>this.fail(e)});return;}
      if(dashboardDescription.length>=10)this.start();
    },error:()=>this.fallbackToAdvanced(id)});
  }
  leafCategories(){const all=this.categories(),parents=new Set(all.flatMap(x=>x.parentId?[x.parentId]:[]));return all.filter(x=>!parents.has(x.id));}
  start(){if(this.input.trim().length<10)return;this.busy.set(true);this.clear();this.service.start(this.input.trim()).subscribe({next:v=>{this.apply(v);void this.router.navigate([], {queryParams:{session:v.sessionId},replaceUrl:true});},error:e=>this.fail(e)});}
  clarify(){if(!this.answer.trim()||!this.result())return;this.busy.set(true);this.clear();this.service.clarify(this.result()!.sessionId,this.answer.trim()).subscribe({next:v=>{this.answer='';this.apply(v);},error:e=>this.fail(e)});}
  approve(){const invalid=this.validate();if(invalid){this.message.set(invalid);this.hasError.set(true);this.scrollError();return;}this.busy.set(true);this.clear();this.service.approve(this.result()!.sessionId,this.toRfqDraft()).subscribe({next:rfq=>void this.router.navigate(['/rfqs',rfq.id,'edit'],{queryParams:{assisted:'saved'}}),error:e=>this.fail(e)});}
  private apply(value:AssistedIntakeResult){this.result.set(value);this.input=value.initialInput;this.draft=structuredClone(value.draft);this.draft.county=this.canonicalCounty(this.draft.county);this.busy.set(false);}
  private validate(){if(!this.draft.title.trim())return'Titlul este obligatoriu.';if(!this.draft.primaryCategoryId)return'Selectează cel puțin o categorie.';if(!this.draft.deliveryAddress.trim())return'Completează adresa de livrare.';if(!this.draft.deliveryLocality.trim()||!this.draft.county.trim())return'Completează localitatea și județul.';if(this.draft.lines.some(x=>!x.name.trim()||!x.quantity||x.quantity<=0||!x.unit))return'Verifică denumirea, cantitatea și unitatea fiecărei poziții.';return'';}
  private toRfqDraft():RfqDraft{const categoryIds=Array.from(new Set([this.draft.primaryCategoryId,...this.draft.lines.map(line=>line.categoryId)].filter((id):id is string=>!!id)));const needType=this.draft.inputType==='ProjectDescription'?'Project':this.draft.inputType==='ShoppingList'?'ShoppingList':'DirectPurchase';return{source:'Assisted',inputType:this.draft.inputType,needType,projectTemplateId:null,categoryIds,title:this.draft.title,projectName:this.draft.projectName,description:this.draft.description,primaryCategoryId:this.draft.primaryCategoryId!,deliveryAddress:this.draft.deliveryAddress,deliveryLocality:this.draft.deliveryLocality,county:this.draft.county,supplierSearchRadiusKm:this.draft.supplierSearchRadiusKm,latitude:null,longitude:null,preferredDeliveryDate:this.draft.preferredDeliveryDate,quotationDeadline:this.draft.quotationDeadline,alternativeProductsAccepted:this.draft.alternativeProductsAccepted,estimatedBudget:this.draft.estimatedBudget,buyerNotes:this.draft.buyerNotes,allowSplit:this.draft.allowSplit,deliveryRequired:this.draft.deliveryRequired,items:this.draft.lines.map(x=>({productName:x.name,manufacturerOrBrand:null,specification:x.specification,quantity:x.quantity!,unit:x.unit!,alternativesAccepted:x.alternativesAccepted,buyerNotes:null,categoryId:x.categoryId,kind:x.kind}))};}
  private fail(error:HttpErrorResponse){if(error.status===503){this.fallbackToAdvanced(this.result()?.sessionId??null);return;}this.busy.set(false);const detail=error.error?.errors?.intake?.[0]??error.error?.detail??'Nu am putut procesa cererea. Încearcă din nou.';this.message.set(detail);this.needsBuyerProfile.set(detail.includes('Completează profilul de cumpărător'));this.hasError.set(true);this.scrollError();}
  private fallbackToAdvanced(sessionId:string|null){void this.router.navigate(['/rfqs/new/advanced'],{replaceUrl:true,queryParams:sessionId?{assistedSession:sessionId}:undefined,state:{message:'Asistentul AI este temporar indisponibil. Poți continua cu formularul complet.'}});}
  private clear(){this.message.set('');this.hasError.set(false);this.needsBuyerProfile.set(false);}
  private scrollError(){setTimeout(()=>this.host.nativeElement.querySelector('[role="alert"]')?.scrollIntoView({behavior:'smooth',block:'center'}));}
  private canonicalCounty(value:string){const key=this.fold(value);return ROMANIAN_COUNTIES.find(county=>this.fold(county)===key)??value;}
  private fold(value:string){return value.normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLocaleLowerCase('ro-RO');}
  private emptyDraft():AssistedDraft{return{inputType:'FreeText',title:'',projectName:null,description:null,primaryCategoryId:null,deliveryAddress:'',deliveryLocality:'',county:'',supplierSearchRadiusKm:30,preferredDeliveryDate:null,quotationDeadline:new Date(Date.now()+172800000).toISOString(),alternativeProductsAccepted:true,allowSplit:false,deliveryRequired:false,estimatedBudget:null,buyerNotes:null,lines:[]};}
}
