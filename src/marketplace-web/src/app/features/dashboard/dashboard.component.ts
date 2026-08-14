import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/auth/auth.service';
import { SupplierOfferListComponent } from '../offers/supplier-offer-list.component';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, FormsModule, SupplierOfferListComponent],
  template: `<section class="need-starter" aria-labelledby="need-starter-title">
      <header><h1 id="need-starter-title">De ce ai nevoie?</h1>
        <p>Descrie ce cauți. Te ajutăm să găsești furnizori relevanți și să compari ofertele.</p></header>
      <section class="general-entry" aria-labelledby="general-entry-title"><div><p class="eyebrow">Începe simplu</p><h2 id="general-entry-title">Caut ceva</h2><p>Produs, material, piesă, serviciu sau echipament.</p></div>
        <label class="sr-only" for="dashboard-need">Descrie pe scurt ce ai nevoie</label><textarea id="dashboard-need" [(ngModel)]="needText" rows="3" placeholder="Ex: Am nevoie de un alternator pentru VW Passat 2018, în Suceava."></textarea>
        <button type="button" (click)="startNeed()" [disabled]="needText.trim().length < 10">Continuă</button>
        <small>Te ajutăm să clarifici detaliile. Nimic nu este trimis fără confirmarea ta.</small>
        <a class="manual-entry" routerLink="/rfqs/new/advanced">Am deja o listă / prefer să completez manual</a>
      </section>
      <section class="construction-entry"><div><h2>Am un proiect de construcții</h2><p>Te ajutăm să structurezi proiectul și achizițiile necesare.</p></div><a routerLink="/rfqs/new/advanced" [queryParams]="{entryMode:'construction'}">Începe proiectul</a></section>
    </section>
    @if(!isBuyer()) {
      <section class="supplier-intro">
        <div><p class="eyebrow">Spațiul furnizorului</p><h1>Transformă cererile potrivite în colaborări.</h1>
          <p>Vezi oportunitățile relevante, răspunde simplu și urmărește fiecare ofertă dintr-un singur loc.</p></div>
        <a routerLink="/open-requests">Explorează cererile <span aria-hidden="true">→</span></a>
      </section>
    }

  <section class="dashboard-content">
      @if(!isBuyer()){<div class="quick-actions">
          <a class="action-card primary" routerLink="/open-requests"><span>↗</span><div><strong>Vezi cererile potrivite</strong><small>Răspunde rapid oportunităților relevante.</small></div><b aria-hidden="true">›</b></a>
          <a class="action-card" routerLink="/account"><span>✓</span><div><strong>Verifică preferințele</strong><small>Categoriile și zonele influențează potrivirile.</small></div><b aria-hidden="true">›</b></a>
      </div>}
      @if(auth.profiles().some(profile => profile.accountType === 'Supplier')){
        <div id="supplier-offers"><app-supplier-offer-list /></div>
        <section class="activity-card"><div><p class="eyebrow">Următorul pas</p><h2>Ține profilul relevant pentru potriviri mai bune</h2>
          <p>Categoriile și zonele de livrare determină cererile pe care le vezi. Semnalele verificate ajută cumpărătorii să decidă cu încredere.</p></div>
          <a routerLink="/account" class="secondary-action">Verifică profilul și preferințele</a></section>
      }
  </section>`,
  styleUrl: './dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DashboardComponent {
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly isBuyer = computed(() => this.auth.isAuthenticated());
  needText = '';

  startNeed(): void {
    const description = this.needText.trim();
    void this.router.navigate(['/rfqs/new'], { queryParams: description ? { description } : undefined });
  }

}
