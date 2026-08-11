import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { SupplierDiscoveryService, SupplierInvitation } from '../../core/supplier-discovery/supplier-discovery.service';

@Component({
  selector:'app-supplier-invitation',
  imports:[DatePipe,RouterLink],
  templateUrl:'./supplier-invitation.component.html',
  styleUrl:'./supplier-invitation.component.scss',
  changeDetection:ChangeDetectionStrategy.OnPush
})
export class SupplierInvitationComponent {
  readonly auth=inject(AuthService);
  private readonly discovery=inject(SupplierDiscoveryService);
  private readonly router=inject(Router);
  private readonly token=inject(ActivatedRoute).snapshot.paramMap.get('token')??'';
  readonly invitation=signal<SupplierInvitation|null>(null);
  readonly loading=signal(true);
  readonly claiming=signal(false);
  readonly error=signal('');
  readonly currentPath=`/supplier-invitations/${encodeURIComponent(this.token)}`;
  readonly hasSupplierRole=computed(()=>this.auth.profiles().some(profile=>profile.accountType==='Supplier'));

  constructor(){this.loadInvitation();}

  loadInvitation():void{
    this.loading.set(true);this.error.set('');
    this.discovery.invitation(this.token).subscribe({
      next:value=>{this.invitation.set(value);this.loading.set(false);},
      error:error=>{this.loading.set(false);this.error.set(error.status===404?'Invitația nu există sau linkul este incorect.':'Invitația nu a putut fi încărcată. Încearcă din nou.');}
    });
  }

  claim():void{
    if(this.claiming())return;
    this.claiming.set(true);this.error.set('');
    this.discovery.claim(this.token).subscribe({
      next:value=>{this.invitation.set(value);this.claiming.set(false);},
      error:error=>{this.claiming.set(false);const messages=error.error?.errors?Object.values(error.error.errors as Record<string,string[]>).flat():[];this.error.set(messages[0]??error.error?.detail??'Firma nu a putut fi revendicată. Verifică profilul de furnizor.');}
    });
  }

  addSupplierProfile():void{
    if(this.claiming())return;
    this.claiming.set(true);this.error.set('');
    this.auth.addProfile('Supplier').subscribe({
      next:()=>void this.router.navigate(['/dashboard'],{queryParams:{section:'settings',returnUrl:this.currentPath}}),
      error:()=>{this.claiming.set(false);this.error.set('Profilul de furnizor nu a putut fi adăugat. Încearcă din Contul meu.');}
    });
  }

  statusMessage(invitation:SupplierInvitation):string{
    return ({Active:'Invitația este activă.',Claimed:'Firma a fost revendicată.',Revoked:'Invitația a fost revocată.',Expired:'Invitația a expirat.'})[invitation.status];
  }
}
