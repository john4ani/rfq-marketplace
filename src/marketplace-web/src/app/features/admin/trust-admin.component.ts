import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ProfileService, AccountTrustSummary } from '../../core/profiles/profile.service';

@Component({ selector: 'app-trust-admin', imports: [FormsModule], changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<section class="admin-page"><p class="eyebrow">Administrare</p><h1>Verificări cont</h1><p>Confirmă numai informațiile verificate printr-un contact sau document real. Nu se afișează scoruri publice.</p>
  <form (ngSubmit)="searchAccounts()"><label>Caută după e-mail <input name="search" [(ngModel)]="search" placeholder="nume@exemplu.ro"></label><button> Caută </button></form>
  @if(loading()) { <p role="status">Se caută conturile…</p> } @else if(error()) { <div class="error" role="alert"><p>{{error()}}</p><button type="button" (click)="searchAccounts()">Încearcă din nou</button></div> }
  @else { @for(account of accounts(); track account.accountId) { <article><h2>{{account.email}}</h2><p>Profil {{account.profileCompletenessPercent}}% · {{account.publishedRequests}} cereri publicate · {{account.submittedOffers}} oferte</p><label><input type="checkbox" [checked]="account.phoneVerified" (change)="review(account, 'phone', $any($event.target).checked)"> Telefon confirmat</label><label><input type="checkbox" [checked]="account.companyVerified" (change)="review(account, 'company', $any($event.target).checked)"> Companie verificată</label></article> } @if(searched() && accounts().length === 0) { <p>Nu am găsit niciun cont.</p> } }</section>`,
  styles:[`.admin-page{max-width:54rem;margin:auto}.admin-page form{display:flex;gap:.75rem;align-items:end;margin:1rem 0}.admin-page label{display:grid;gap:.35rem}.admin-page article{border:1px solid #d8dce5;border-radius:.75rem;padding:1rem;margin:.75rem 0}.admin-page article label{display:inline-flex;margin-right:1rem}`] })
export class TrustAdminComponent {
  private readonly profiles=inject(ProfileService); search=''; readonly accounts=signal<AccountTrustSummary[]>([]); readonly loading=signal(false); readonly error=signal(''); readonly searched=signal(false);
  searchAccounts():void { this.loading.set(true);this.error.set('');this.accounts.set([]);this.searched.set(true);this.profiles.searchTrustAccounts(this.search).subscribe({next:v=>{this.accounts.set(v);this.loading.set(false);},error:()=>{this.error.set('Conturile nu au putut fi încărcate.');this.loading.set(false);}}); }
  review(account:AccountTrustSummary, field:'phone'|'company', checked:boolean):void { this.error.set('');this.profiles.reviewTrustAccount(account.accountId,{phoneVerified:field==='phone'?checked:account.phoneVerified,companyVerified:field==='company'?checked:account.companyVerified}).subscribe({next:value=>this.accounts.update(items=>items.map(item=>item.accountId===value.accountId?value:item)),error:()=>this.error.set('Verificarea nu a putut fi salvată.')}); }
}
