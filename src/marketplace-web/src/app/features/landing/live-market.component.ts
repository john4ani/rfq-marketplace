import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnDestroy, computed, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { LiveMarketService, LiveParticipant } from '../../core/marketplace/live-market.service';
import { OpenRfq, OpenRfqService } from '../../core/rfqs/open-rfq.service';

@Component({
  selector:'app-live-market',
  imports:[RouterLink,DatePipe],
  template:`<section class="live-hero">
    <div><p class="eyebrow">Piața cererilor și ofertelor</p><h1>Piața Live</h1>
      <p class="hero-copy">Găsește cereri relevante, trimite oferte și dezvoltă parteneriate în zona ta.</p>
      <div class="hero-actions">
        @if(auth.isAuthenticated()){
          @if(hasSupplierProfile()){<a class="primary-action" routerLink="/open-requests">Explorează cererile</a>}
          @if(hasBuyerProfile()){<a class="secondary-action" routerLink="/dashboard">Cererile mele</a>}
        }@else{<a class="primary-action" routerLink="/register">Începe acum</a><a class="secondary-action" routerLink="/sign-in">Autentificare</a>}
      </div>
    </div>
    <div class="market-pulse"><span>Activitate în piață</span><strong>{{onlineCount()}} online acum</strong>
      <small>{{buyers().length}} cumpărători · {{suppliers().length}} furnizori</small>
      @if(auth.isAuthenticated()){
        <div class="my-presence"><i aria-hidden="true"></i><div><small>Tu ești online ca</small><b>{{presenceRoleLabel()}}</b></div></div>
      }
    </div>
  </section>

  <section class="open-requests-home" aria-labelledby="open-requests-title">
    <div class="open-requests-heading"><div><p class="eyebrow">Cereri de ofertă</p>
      <h2 id="open-requests-title">{{hasSupplierProfile()?'Recomandate pentru tine':'Oportunități recente'}}</h2>
      <p>{{hasSupplierProfile()?'Potrivite categoriilor și zonei tale de livrare.':'Conectăm nevoile cumpărătorilor cu furnizorii potriviți.'}}</p></div>
      @if(hasSupplierProfile()){<a class="section-link" routerLink="/open-requests">Vezi toate →</a>}
    </div>
    @if(hasSupplierProfile()){
      @if(requestsLoading()){<div class="request-skeleton" role="status"><span></span><span></span><span></span></div>}
      @else if(requestsError()){<p class="open-request-state error">{{requestsError()}}</p>}
      @else{<div class="open-request-grid">
        @for(request of openRequests().slice(0,3);track request.id){
          <a class="open-request-card" [routerLink]="['/open-requests',request.id]">
            <div><span>{{request.categoryName}}</span><small>{{request.deliveryLocality}}, {{request.county}}</small></div>
            <h3>{{request.title}}</h3>
            <div class="request-meta"><span>{{request.items.length||'Generală'}} {{request.items.length===1?'poziție':'poziții'}}</span>
              @if(request.estimatedBudget!==null){<span>{{request.estimatedBudget}} lei</span>}</div>
            <p>Termen {{request.quotationDeadline|date:'dd.MM, HH:mm'}}</p><strong>Vezi cererea →</strong>
          </a>
        }@empty{<div class="open-request-state empty-action"><strong>Nicio recomandare momentan</strong>
          <span>Actualizează categoriile și zona de livrare pentru rezultate mai bune.</span><a routerLink="/dashboard">Configurează preferințele</a></div>}
      </div>}
    }@else{<div class="open-request-callout"><div><strong>Descoperă cereri potrivite activității tale</strong>
      <p>Completează profilul de furnizor pentru recomandări personalizate.</p></div><a routerLink="/dashboard">Configurează profilul</a></div>}
  </section>

  <section class="community-preview">
    <div><p class="eyebrow">Comunitate</p><h2>Parteneri activi în piață</h2>
      <p>Vezi cumpărătorii și furnizorii disponibili, filtrați după locație și status.</p></div>
    <div class="community-counts"><span><strong>{{onlineBuyers()}}</strong> cumpărători online</span>
      <span><strong>{{onlineSuppliers()}}</strong> furnizori online</span></div>
    <a routerLink="/participants">Deschide directorul →</a>
  </section>`,
  styleUrl:'./live-market.component.scss',
  changeDetection:ChangeDetectionStrategy.OnPush
})
export class LiveMarketComponent implements OnDestroy{
  readonly auth=inject(AuthService);private readonly market=inject(LiveMarketService);private readonly openRfqService=inject(OpenRfqService);
  readonly participants=signal<LiveParticipant[]>([]);readonly openRequests=signal<OpenRfq[]>([]);readonly requestsLoading=signal(false);readonly requestsError=signal('');
  readonly hasSupplierProfile=computed(()=>this.auth.profiles().some(p=>p.accountType==='Supplier'));
  readonly hasBuyerProfile=computed(()=>this.auth.profiles().some(p=>p.accountType==='Buyer'));
  readonly buyers=computed(()=>this.participants().filter(p=>p.profileType==='Buyer'));
  readonly suppliers=computed(()=>this.participants().filter(p=>p.profileType==='Supplier'));
  readonly onlineCount=computed(()=>this.participants().filter(p=>p.isOnline).length);
  readonly onlineBuyers=computed(()=>this.buyers().filter(p=>p.isOnline).length);
  readonly onlineSuppliers=computed(()=>this.suppliers().filter(p=>p.isOnline).length);
  readonly presenceRoleLabel=computed(()=>{
    const buyer=this.hasBuyerProfile(),supplier=this.hasSupplierProfile();
    return buyer&&supplier?'Cumpărător și Furnizor':supplier?'Furnizor':buyer?'Cumpărător':'utilizator conectat';
  });
  private readonly refreshTimer:ReturnType<typeof setInterval>;private requestsLoaded=false;
  constructor(){this.loadMarket();effect(()=>{if(this.hasSupplierProfile()&&!this.requestsLoaded){this.requestsLoaded=true;this.loadRequests();}});this.refreshTimer=setInterval(()=>this.loadMarket(),30_000);}
  ngOnDestroy(){clearInterval(this.refreshTimer);}
  private loadMarket(){this.market.getSnapshot(this.auth.session()?.accountType).subscribe({next:value=>this.participants.set(value.participants),error:()=>undefined});}
  private loadRequests(){this.requestsLoading.set(true);this.openRfqService.list().subscribe({next:value=>{this.openRequests.set(value);this.requestsLoading.set(false);},error:()=>{this.requestsLoading.set(false);this.requestsError.set('Recomandările nu au putut fi încărcate.');}});}
}
