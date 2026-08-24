import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnDestroy, computed, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { LiveMarketService, LiveParticipant } from '../../core/marketplace/live-market.service';
import { OpenRfq, OpenRfqService } from '../../core/rfqs/open-rfq.service';
import { LanguageService } from '../../core/i18n/language.service';

@Component({
  selector:'app-live-market',
  imports:[RouterLink,DatePipe],
  template:`<section class="live-hero">
    <div><p class="eyebrow">{{ i18n.t('Piața cererilor și ofertelor') }}</p><h1>{{ i18n.t('Piața Live') }}</h1>
      <p class="hero-copy">{{ i18n.t('Găsește cereri relevante, trimite oferte și dezvoltă parteneriate în zona ta.') }}</p>
      <div class="hero-actions">
        @if(auth.isAuthenticated()){
          @if(hasSupplierProfile()){<a class="primary-action" routerLink="/open-requests">{{ i18n.t('Explorează cererile') }}</a>}
          <a class="secondary-action" routerLink="/rfqs/new">{{ i18n.t('Cerere nouă') }}</a>
        }@else{<a class="primary-action" routerLink="/register">{{ i18n.t('Începe acum') }}</a><a class="secondary-action" routerLink="/sign-in">{{ i18n.t('Autentificare') }}</a>}
      </div>
    </div>
    <div class="market-pulse"><span>{{ i18n.t('Activitate în piață') }}</span><strong>{{onlineCount()}} {{ i18n.t('online acum') }}</strong>
      <small>{{buyers().length}} {{ i18n.t('cumpărători') }} · {{suppliers().length}} {{ i18n.t('furnizori') }}</small>
      @if(auth.isAuthenticated()){
        <div class="my-presence"><i aria-hidden="true"></i><div><small>{{ i18n.t('Tu ești online ca') }}</small><b>{{ i18n.t(presenceRoleLabel()) }}</b></div></div>
      }
    </div>
    @if(marketError()){<section class="market-error" role="alert"><strong>{{ i18n.t('Activitatea pieței nu a putut fi actualizată.') }}</strong><button type="button" (click)="retryMarket()">{{ i18n.t('Încearcă din nou') }}</button></section>}
  </section>

  <section class="open-requests-home" aria-labelledby="open-requests-title">
    <div class="open-requests-heading"><div><p class="eyebrow">{{ i18n.t('Cereri de ofertă') }}</p>
      <h2 id="open-requests-title">{{ i18n.t(hasSupplierProfile()?'Recomandate pentru tine':'Oportunități recente') }}</h2>
      <p>{{ i18n.t(hasSupplierProfile()?'Potrivite categoriilor și zonei tale de livrare.':'Conectăm nevoile cumpărătorilor cu furnizorii potriviți.') }}</p></div>
      @if(hasSupplierProfile()){<a class="section-link" routerLink="/open-requests">{{ i18n.t('Vezi toate →') }}</a>}
    </div>
    @if(hasSupplierProfile()){
      @if(requestsLoading()){<div class="request-skeleton" role="status"><span></span><span></span><span></span></div>}
      @else if(requestsError()){<div class="open-request-state error" role="alert"><span>{{requestsError()}}</span><button type="button" (click)="retryRequests()">{{ i18n.t('Încearcă din nou') }}</button></div>}
      @else{<div class="open-request-grid">
        @for(request of openRequests().slice(0,3);track request.id){
          <a class="open-request-card" [routerLink]="['/open-requests',request.id]">
            <div><span>{{request.categoryName}}</span><small>{{request.deliveryLocality}}, {{request.county}}</small></div>
            <h3>{{request.title}}</h3>
            <div class="request-meta"><span>{{request.items.length||i18n.t('Generală')}} {{ i18n.t(request.items.length===1?'poziție':'poziții') }}</span>
              @if(request.estimatedBudget!==null){<span>{{request.estimatedBudget}} lei</span>}</div>
            <p>{{ i18n.t('Termen') }} {{request.quotationDeadline|date:'dd.MM, HH:mm'}}</p><strong>{{ i18n.t('Vezi cererea →') }}</strong>
          </a>
        }@empty{<div class="open-request-state empty-action"><strong>{{ i18n.t('Nicio recomandare momentan') }}</strong>
        <span>{{ i18n.t('Actualizează categoriile și zona de livrare pentru rezultate mai bune.') }}</span><a routerLink="/account">{{ i18n.t('Configurează preferințele') }}</a></div>}
      </div>}
    }@else{<div class="open-request-callout"><div><strong>{{ i18n.t('Descoperă cereri potrivite activității tale') }}</strong>
      <p>{{ i18n.t('Completează profilul de furnizor pentru recomandări personalizate.') }}</p></div><a routerLink="/account">{{ i18n.t('Configurează profilul') }}</a></div>}
  </section>

  <section class="community-preview">
    <div><p class="eyebrow">{{ i18n.t('Comunitate') }}</p><h2>{{ i18n.t('Parteneri activi în piață') }}</h2>
      <p>{{ i18n.t('Vezi cumpărătorii și furnizorii disponibili, filtrați după locație și status.') }}</p></div>
    <div class="community-counts"><span><strong>{{onlineBuyers()}}</strong> {{ i18n.t('cumpărători online') }}</span>
      <span><strong>{{onlineSuppliers()}}</strong> {{ i18n.t('furnizori online') }}</span></div>
    <a routerLink="/participants">{{ i18n.t('Deschide directorul →') }}</a>
  </section>`,
  styleUrl:'./live-market.component.scss',
  changeDetection:ChangeDetectionStrategy.OnPush
})
export class LiveMarketComponent implements OnDestroy{
  readonly auth=inject(AuthService);private readonly market=inject(LiveMarketService);private readonly openRfqService=inject(OpenRfqService);
  readonly i18n=inject(LanguageService);
  readonly participants=signal<LiveParticipant[]>([]);readonly openRequests=signal<OpenRfq[]>([]);readonly requestsLoading=signal(false);readonly requestsError=signal('');readonly marketError=signal(false);
  readonly hasSupplierProfile=computed(()=>this.auth.profiles().some(p=>p.accountType==='Supplier'));
  readonly buyers=computed(()=>this.participants().filter(p=>p.profileType==='Buyer'));
  readonly suppliers=computed(()=>this.participants().filter(p=>p.profileType==='Supplier'));
  readonly onlineCount=signal(0);
  readonly onlineBuyers=signal(0);
  readonly onlineSuppliers=signal(0);
  readonly presenceRoleLabel=computed(()=>{
    return this.hasSupplierProfile() ? 'Cumpărător și Furnizor' : 'utilizator conectat';
  });
  private readonly refreshTimer:ReturnType<typeof setInterval>;private requestsLoaded=false;
  constructor(){this.loadMarket();effect(()=>{if(this.hasSupplierProfile()&&!this.requestsLoaded){this.requestsLoaded=true;this.loadRequests();}});this.refreshTimer=setInterval(()=>this.loadMarket(),30_000);}
  ngOnDestroy(){clearInterval(this.refreshTimer);}
  retryMarket(){this.loadMarket();}
  retryRequests(){this.loadRequests();}
  private loadMarket(){this.market.getSnapshot(this.auth.session()?.accountType).subscribe({next:value=>{
    this.participants.set(value.participants);
    this.onlineCount.set(value.onlineUserCount);
    this.onlineBuyers.set(value.onlineBuyerCount);
    this.onlineSuppliers.set(value.onlineSupplierCount);
    this.marketError.set(false);
  },error:()=>this.marketError.set(true)});}
  private loadRequests(){this.requestsLoading.set(true);this.requestsError.set('');this.openRfqService.list().subscribe({next:value=>{this.openRequests.set(value);this.requestsLoading.set(false);},error:()=>{this.requestsLoading.set(false);this.requestsError.set(this.i18n.t('Recomandările nu au putut fi încărcate.'));}});}
}
