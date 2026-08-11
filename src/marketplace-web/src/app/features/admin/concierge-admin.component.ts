import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ConciergeOverview, ConciergeRequest, ConciergeService, ConciergeSupplier } from '../../core/concierge/concierge.service';
import { ProcurementOutcome } from '../../core/rfqs/rfq.service';

@Component({selector:'app-concierge-admin',imports:[FormsModule,DatePipe],changeDetection:ChangeDetectionStrategy.OnPush,templateUrl:'./concierge-admin.component.html',styleUrl:'./concierge-admin.component.scss'})
export class ConciergeAdminComponent {
  private readonly concierge=inject(ConciergeService); readonly data=signal<ConciergeOverview|null>(null); readonly error=signal(''); readonly loading=signal(true); readonly selected=signal<ConciergeRequest|null>(null); readonly suppliers=signal<ConciergeSupplier[]>([]); note=''; supplierSearch=''; selectedSupplierId=''; outcome:ProcurementOutcome='NoSuitableOffer';
  constructor(){this.load();}
  load(){this.loading.set(true);this.error.set('');this.data.set(null);this.concierge.overview().subscribe({next:value=>{this.data.set(value);this.loading.set(false);},error:()=>{this.error.set('Spațiul de administrare nu a putut fi încărcat.');this.loading.set(false);}});}
  select(request:ConciergeRequest){this.selected.set(request);this.note='';this.selectedSupplierId='';this.suppliers.set([]);this.error.set('');}
  findSuppliers(){this.concierge.suppliers(this.supplierSearch).subscribe({next:v=>this.suppliers.set(v),error:()=>this.error.set('Furnizorii nu au putut fi căutați.')});}
  manualMatch(){const request=this.selected();if(!request||!this.selectedSupplierId)return;this.action(this.concierge.manualMatch(request.id,this.selectedSupplierId,this.note));}
  correction(){const request=this.selected();if(!request)return;this.action(this.concierge.correction(request.id,this.note));}
  recordOutcome(){const request=this.selected();if(!request)return;this.action(this.concierge.outcome(request.id,this.outcome,this.note));}
  normalize(id:string){const note=prompt('Explică furnizorului ce trebuie clarificat (5-1000 caractere).')||'';if(note.length<5)return;this.action(this.concierge.normalize(id,note));}
  private action(request:{subscribe:(handlers:{next:()=>void;error:(error:{error?:{errors?:Record<string,string[]>}})=>void})=>void}){this.error.set('');request.subscribe({next:()=>{this.selected.set(null);this.load();},error:error=>this.error.set(Object.values(error.error?.errors??{})[0]?.[0]??'Acțiunea nu a putut fi finalizată.')});}
}
