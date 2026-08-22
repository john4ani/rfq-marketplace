import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SupplierDiscoveryService, DiscoveryOpportunity } from '../../core/supplier-discovery/supplier-discovery.service';

@Component({
  selector:'app-supplier-discovery-admin',
  imports:[RouterLink],
  templateUrl:'./supplier-discovery-admin.component.html',
  styleUrl:'./supplier-discovery-admin.component.scss',
  changeDetection:ChangeDetectionStrategy.OnPush
})
export class SupplierDiscoveryAdminComponent {
  private readonly discovery=inject(SupplierDiscoveryService);
  readonly opportunities=signal<DiscoveryOpportunity[]>([]);
  readonly loading=signal(true);
  readonly error=signal('');

  constructor(){this.load();}

  load():void{
    this.loading.set(true);this.error.set('');
    this.discovery.opportunities().subscribe({
      next:opportunities=>{this.opportunities.set(opportunities);this.loading.set(false);},
      error:()=>{this.loading.set(false);this.error.set('Cererile pentru Supplier Discovery nu au putut fi încărcate.');}
    });
  }
}
