import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/auth/auth.service';
import { SupplierOfferListComponent } from '../offers/supplier-offer-list.component';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, FormsModule, SupplierOfferListComponent],
  template: `@if(isBuyer()) {
      <section class="need-starter" aria-labelledby="need-starter-title">
        <div><h1 id="need-starter-title">Bună! Cu ce te putem ajuta?</h1>
          <p>Scrie-ne ce ai nevoie, așa cum i-ai spune unui furnizor.</p></div>
        <div class="need-entry"><label class="sr-only" for="dashboard-need">Descrie pe scurt ce ai nevoie</label><input id="dashboard-need" [(ngModel)]="needText" (keyup.enter)="startNeed()" placeholder="Am nevoie de cherestea pentru un acoperiș de 10 × 8 m"><button type="button" (click)="startNeed()">Continuă</button></div>
        <div class="starter-reassurance"><span>AI-ul te ajută să clarifici</span><span>Nimic nu este trimis fără confirmarea ta</span></div>
        <a class="manual-entry" routerLink="/rfqs/new/advanced">Am deja lista / completez manual</a>
      </section>
    } @else {
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
