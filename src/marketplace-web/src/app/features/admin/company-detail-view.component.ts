import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CompanyRegistryEntry, CompanyRegistryService } from '../../core/companies/company-registry.service';

@Component({selector:'app-company-detail-view',imports:[RouterLink],changeDetection:ChangeDetectionStrategy.OnPush,template:`
<section class="page">
  <a routerLink="/admin/companies">← Înapoi la firme</a>
  @if (loading()) { <section class="state" role="status">Se încarcă firma…</section> }
  @else if (company(); as item) {
    <header><h1>{{ item.name }}</h1><p>{{ status(item) }}</p></header>
    <section class="card facts">
      @if (item.taxIdentifier) { <div><span>CUI</span><strong>{{ item.taxIdentifier }}</strong></div> }
      <div><span>Localitate</span><strong>{{ item.locality }}, {{ item.county }}</strong></div>
      @if (item.phone) { <div><span>Telefon</span><strong>{{ item.phone }}</strong></div> }
      @if (item.email) { <div><span>E-mail</span><strong>{{ item.email }}</strong></div> }
      @if (item.website) { <div><span>Website</span><strong>{{ item.website }}</strong></div> }
    </section>
    <section class="card"><h2>Ce pare să ofere</h2><p>{{ item.categories.join(' · ') || 'Nu avem încă informații despre categorii.' }}</p></section>
    @if (item.kind === 'Discovered') { <div class="actions"><a class="secondary" routerLink="/admin/supplier-discovery">Vezi potrivirile</a><a [routerLink]="'/admin/supplier-discovery/acquisition'" [queryParams]="{candidateId:item.id}">Vezi în Achiziție furnizori</a></div> }
  } @else { <section class="state">Firma nu este disponibilă.</section> }
</section>`,styles:[`.page{display:grid;gap:1rem;max-width:48rem}.card,.state{padding:1.2rem;border:1px solid var(--border);border-radius:1rem;background:var(--surface)}.facts{display:grid;gap:.7rem}.facts div{display:grid;gap:.15rem}.facts span{color:var(--muted);font-size:.9rem}.actions{display:flex;gap:.7rem;flex-wrap:wrap}`]})
export class CompanyDetailViewComponent{private readonly api=inject(CompanyRegistryService);private readonly route=inject(ActivatedRoute);readonly company=signal<CompanyRegistryEntry|null>(null);readonly loading=signal(true);constructor(){const id=this.route.snapshot.paramMap.get('id');const kind=this.route.snapshot.paramMap.get('kind');this.api.list().subscribe({next:items=>{this.company.set(items.find(item=>item.id===id&&item.kind===kind)||null);this.loading.set(false);},error:()=>this.loading.set(false)});}status(item:CompanyRegistryEntry){return item.kind==='Registered'?'Firmă înregistrată':item.claimed?'Firmă descoperită · revendicată':'Firmă descoperită · nerevendicată';}}
