import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DatePipe, DOCUMENT } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { SupplierDiscoveryService, SupplierCandidate, DiscoveryOpportunity } from '../../core/supplier-discovery/supplier-discovery.service';
import { CategoryOption, SupplierSettingsService } from '../../core/suppliers/supplier-settings.service';
import { ROMANIAN_COUNTIES } from '../../shared/romanian-counties';

@Component({
  selector:'app-supplier-discovery-admin',
  imports:[ReactiveFormsModule,DatePipe,RouterLink],
  templateUrl:'./supplier-discovery-admin.component.html',
  styleUrl:'./supplier-discovery-admin.component.scss',
  changeDetection:ChangeDetectionStrategy.OnPush
})
export class SupplierDiscoveryAdminComponent {
  private readonly discovery=inject(SupplierDiscoveryService);
  private readonly settings=inject(SupplierSettingsService);
  private readonly document=inject(DOCUMENT);
  readonly counties=ROMANIAN_COUNTIES;
  readonly candidates=signal<SupplierCandidate[]>([]);
  readonly opportunities=signal<DiscoveryOpportunity[]>([]);
  readonly categories=signal<CategoryOption[]>([]);
  readonly selectedCategoryIds=signal<string[]>([]);
  readonly loading=signal(true);
  readonly saving=signal(false);
  readonly message=signal('');
  readonly error=signal('');
  readonly reviewingId=signal<string|null>(null);
  readonly invitingId=signal<string|null>(null);
  readonly reviewNotes=new FormControl('',{nonNullable:true,validators:[Validators.required,Validators.minLength(5),Validators.maxLength(1000)]});
  readonly opportunityId=new FormControl('',{nonNullable:true,validators:Validators.required});
  readonly createdInvitation=signal<{companyName:string;link:string;expiresAt:string}|null>(null);
  readonly invitationError=signal<{candidateId:string;message:string}|null>(null);
  readonly form=new FormGroup({
    companyName:new FormControl('',{nonNullable:true,validators:[Validators.required,Validators.minLength(2),Validators.maxLength(180)]}),
    contactEmail:new FormControl('',{nonNullable:true,validators:[Validators.email,Validators.maxLength(254)]}),
    phoneNumber:new FormControl('',{nonNullable:true,validators:Validators.maxLength(40)}),
    website:new FormControl('',{nonNullable:true,validators:Validators.maxLength(500)}),
    city:new FormControl('',{nonNullable:true,validators:[Validators.required,Validators.minLength(2),Validators.maxLength(120)]}),
    county:new FormControl('',{nonNullable:true,validators:Validators.required}),
    source:new FormControl('',{nonNullable:true,validators:[Validators.required,Validators.minLength(3),Validators.maxLength(500)]}),
    notes:new FormControl('',{nonNullable:true,validators:Validators.maxLength(1000)})
  });

  constructor(){this.load();}

  createCandidate():void{
    if(this.form.invalid||!this.selectedCategoryIds().length||this.saving()){this.form.markAllAsTouched();this.error.set('Completează denumirea, localitatea, județul, cel puțin o categorie și sursa informațiilor.');return;}
    this.saving.set(true);this.error.set('');this.message.set('');
    const value=this.form.getRawValue();
    this.discovery.createCandidate({
      ...value,
      categoryIds:this.selectedCategoryIds(),
      contactEmail:value.contactEmail.trim()||null,
      phoneNumber:value.phoneNumber.trim()||null,
      website:value.website.trim()||null,
      notes:value.notes.trim()||null
    }).subscribe({
      next:candidate=>{this.candidates.update(values=>[candidate,...values]);this.form.reset();this.selectedCategoryIds.set([]);this.saving.set(false);this.message.set('Firma a fost salvată ca profil descoperit și neverificat.');},
      error:error=>{this.saving.set(false);this.error.set(this.apiError(error,'Firma nu a putut fi salvată.'));}
    });
  }

  startReview(id:string):void{this.reviewingId.set(id);this.invitingId.set(null);this.reviewNotes.reset();this.error.set('');}
  cancelReview():void{this.reviewingId.set(null);this.reviewNotes.reset();}
  review(candidate:SupplierCandidate,approved:boolean):void{
    if(this.reviewNotes.invalid||this.saving())return;
    this.saving.set(true);this.error.set('');
    this.discovery.reviewCandidate(candidate.id,approved,this.reviewNotes.value.trim()).subscribe({
      next:updated=>{this.replace(updated);this.saving.set(false);this.reviewingId.set(null);this.message.set(approved?'Relevanța a fost validată. Firma rămâne marcată ca nerevendicată.':'Firma a fost respinsă și nu poate fi invitată.');},
      error:error=>{this.saving.set(false);this.error.set(this.apiError(error,'Validarea nu a putut fi salvată.'));}
    });
  }

  startInvitation(id:string):void{this.invitingId.set(id);this.reviewingId.set(null);this.opportunityId.reset();this.error.set('');this.invitationError.set(null);}
  cancelInvitation():void{this.invitingId.set(null);this.opportunityId.reset();this.invitationError.set(null);}
  // Invitations are not category-bound; the company's categories come from the Registration process. Any open
  // request may be selected for an invitation, so the picker lists every request that is currently receiving offers.
  compatibleOpportunities(_candidate:SupplierCandidate):DiscoveryOpportunity[]{
    return this.opportunities();
  }
  toggleCategory(id:string):void{const values=this.selectedCategoryIds();this.selectedCategoryIds.set(values.includes(id)?values.filter(value=>value!==id):[...values,id]);}
  candidateCategoryNames(candidate:SupplierCandidate):string{return candidate.categories.map(category=>category.name).join(', ');}
  createInvitation(candidate:SupplierCandidate):void{
    if(this.opportunityId.invalid||this.saving())return;
    this.saving.set(true);this.error.set('');this.invitationError.set(null);this.createdInvitation.set(null);
    this.discovery.createInvitation(candidate.id,this.opportunityId.value).subscribe({
      next:result=>{
        const link=`${window.location.origin}/supplier-invitations/${encodeURIComponent(result.token)}`;
        this.createdInvitation.set({companyName:candidate.companyName,link,expiresAt:result.expiresAt});
        this.saving.set(false);this.invitingId.set(null);this.message.set('Invitația a fost pregătită. Trimite linkul numai după verificarea finală a destinatarului.');
        this.loadCandidates();
        this.focusFeedback('created-invitation');
      },
      error:error=>{
        const message=this.apiError(error,'Invitația nu a putut fi creată. Încearcă din nou.');
        this.saving.set(false);
        this.invitationError.set({candidateId:candidate.id,message});
        this.focusFeedback(`invitation-opportunity-${candidate.id}`);
      }
    });
  }

  async copyInvitation():Promise<void>{
    const invitation=this.createdInvitation();
    if(!invitation)return;
    try{await navigator.clipboard.writeText(invitation.link);this.message.set('Linkul invitației a fost copiat.');}
    catch{this.message.set('Selectează și copiază manual linkul invitației.');}
  }

  statusLabel(candidate:SupplierCandidate):string{
    return ({Unverified:'Date neverificate',EligibleForInvitation:'Relevanță validată · nerevendicată',Rejected:'Respinsă',Claimed:'Revendicată'})[candidate.status];
  }
  invitationStatus(status:SupplierCandidate['invitations'][number]['status']):string{
    return ({Active:'Activă',Claimed:'Revendicată',Revoked:'Revocată',Expired:'Expirată'})[status];
  }

  private load():void{
    forkJoin({candidates:this.discovery.candidates(),opportunities:this.discovery.opportunities(),categories:this.settings.getCategories()}).subscribe({
      next:result=>{this.candidates.set(result.candidates);this.opportunities.set(result.opportunities);this.categories.set(result.categories);this.loading.set(false);},
      error:error=>{this.loading.set(false);this.error.set(this.apiError(error,'Datele de discovery nu au putut fi încărcate.'));}
    });
  }
  private loadCandidates():void{this.discovery.candidates().subscribe({next:values=>this.candidates.set(values)});}
  private replace(updated:SupplierCandidate):void{this.candidates.update(values=>values.map(value=>value.id===updated.id?updated:value));}
  private focusFeedback(id:string):void{
    setTimeout(()=>{
      const feedback=this.document.getElementById(id);
      feedback?.scrollIntoView?.({behavior:'smooth',block:'center'});
      feedback?.focus();
    });
  }
  private apiError(error:{status?:number;error?:{errors?:Record<string,string[]>;detail?:string}},fallback:string):string{
    const messages=error.error?.errors?Object.values(error.error.errors).flat():[];
    if(messages.length)return messages.join(' ');
    if(error.error?.detail)return error.error.detail;
    if(error.status===403)return 'Contul nu are rolul Administrator.';
    return fallback;
  }
}
