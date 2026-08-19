import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ConciergeOffer, ConciergeOverview, ConciergeRequest, ConciergeService, ConciergeSupplier } from '../../core/concierge/concierge.service';
import { ProcurementOutcome } from '../../core/rfqs/rfq.service';

@Component({selector:'app-concierge-admin',imports:[FormsModule,DatePipe],changeDetection:ChangeDetectionStrategy.OnPush,templateUrl:'./concierge-admin.component.html',styleUrl:'./concierge-admin.component.scss'})
export class ConciergeAdminComponent {
  private readonly concierge=inject(ConciergeService); readonly data=signal<ConciergeOverview|null>(null); readonly error=signal(''); readonly loading=signal(true); readonly selected=signal<ConciergeRequest|null>(null); readonly selectedOffer=signal<ConciergeOffer|null>(null); readonly suppliers=signal<ConciergeSupplier[]>([]); readonly saving=signal(false); note=''; supplierSearch=''; selectedSupplierId=''; outcome:ProcurementOutcome='NoSuitableOffer';
  constructor(){this.load();}
  load(){this.loading.set(true);this.error.set('');this.data.set(null);this.concierge.overview().subscribe({next:value=>{this.data.set(value);this.loading.set(false);},error:()=>{this.error.set('Spațiul de administrare nu a putut fi încărcat.');this.loading.set(false);}});}
  select(request:ConciergeRequest){this.selected.set(request);this.note='';this.selectedSupplierId='';this.suppliers.set([]);this.error.set('');}
  selectOffer(offer:ConciergeOffer){this.selectedOffer.set(offer);this.note='';this.error.set('');}
  findSuppliers(){this.concierge.suppliers(this.supplierSearch).subscribe({next:v=>this.suppliers.set(v),error:()=>this.error.set('Furnizorii nu au putut fi căutați.')});}
  manualMatch(){const request=this.selected();if(!request||!this.selectedSupplierId)return;this.action(this.concierge.manualMatch(request.id,this.selectedSupplierId,this.note));}
  correction(){const request=this.selected();if(!request)return;this.action(this.concierge.correction(request.id,this.note));}
  recordOutcome(){const request=this.selected();if(!request)return;this.action(this.concierge.outcome(request.id,this.outcome,this.note));}
  requestNormalization(){const offer=this.selectedOffer();if(!offer||this.note.trim().length<5)return;this.action(this.concierge.normalize(offer.id,this.note.trim()),true);}
  private action(request:{subscribe:(handlers:{next:()=>void;error:(error:{error?:{errors?:Record<string,string[]>;detail?:string}})=>void})=>void},closeOffer=false){if(this.saving())return;this.saving.set(true);this.error.set('');request.subscribe({next:()=>{this.selected.set(null);if(closeOffer)this.selectedOffer.set(null);this.saving.set(false);this.load();},error:error=>{this.saving.set(false);this.error.set(Object.values(error.error?.errors??{})[0]?.[0]??error.error?.detail??'Acțiunea nu a putut fi finalizată.');}});}
}
