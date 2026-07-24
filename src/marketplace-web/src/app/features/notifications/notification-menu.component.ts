import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, ElementRef, HostListener, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AppNotification, NotificationService } from '../../core/notifications/notification.service';

@Component({
  selector:'app-notification-menu',
  imports:[DatePipe],
  template:`<div class="notifications">
    <button class="bell" type="button" (click)="toggle()" [attr.aria-expanded]="open()" aria-label="Notificări">
      <svg aria-hidden="true" viewBox="0 0 24 24"><path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9M10 21h4"/></svg>
      @if(service.unreadCount()>0){<span class="count">{{service.unreadCount()>99?'99+':service.unreadCount()}}</span>}
    </button>
    @if(open()){
      <section class="panel" aria-label="Panou notificări">
        <header><div><strong>Notificări</strong><small>{{service.unreadCount()}} necitite</small></div>
          @if(service.unreadCount()>0){<button type="button" (click)="service.markAllRead()">Marchează toate</button>}</header>
        @if(service.loading()){<p class="state">Se încarcă notificările…</p>}
        @if(!service.loading()&&service.notifications().length===0){<p class="state">Nu ai notificări încă.</p>}
        <div class="list">@for(item of service.notifications();track item.id){
          <button type="button" class="item" [class.unread]="!item.isRead" (click)="openNotification(item)">
            <span class="dot" aria-hidden="true"></span><span><strong>{{item.title}}</strong><span>{{item.message}}</span>
              <small>{{item.createdAt|date:'dd.MM.yyyy, HH:mm'}}</small></span>
          </button>
        }</div>
      </section>
    }
  </div>`,
  styles:[`.notifications{position:relative}.bell{position:relative;display:grid;width:2.5rem;height:2.5rem;place-items:center;padding:0!important;border-radius:.55rem!important;color:#33443a!important;background:#edf3ef!important}.bell svg{width:1.2rem;fill:none;stroke:currentColor;stroke-width:1.8;stroke-linecap:round;stroke-linejoin:round}.count{position:absolute;top:-.25rem;right:-.3rem;display:grid;min-width:1.1rem;height:1.1rem;place-items:center;padding:0 .15rem;border:2px solid white;border-radius:99px;background:#c52f3f;color:white;font-size:.65rem}.panel{position:absolute;top:calc(100% + .65rem);right:0;width:min(23rem,calc(100vw - 1.5rem));overflow:hidden;border:1px solid #dce4df;border-radius:14px;background:white;box-shadow:0 18px 50px #17251c2b}.panel header{display:flex;align-items:center;justify-content:space-between;padding:1rem;border-bottom:1px solid #e8ece9}.panel header div{display:grid}.panel header small{color:#68746d}.panel header button{padding:.35rem!important;color:#176b52!important;background:transparent!important;font-size:.8rem}.list{max-height:24rem;overflow:auto}.item{display:grid!important;width:100%;grid-template-columns:.55rem 1fr;gap:.45rem;padding:.85rem 1rem!important;border-radius:0!important;color:#233129!important;background:white!important;text-align:left}.item:hover{background:#f5f8f6!important}.item.unread{background:#edf7f2!important}.item>span:last-child{display:grid;gap:.2rem}.item span span,.item small{color:#627068;font-size:.82rem;font-weight:400}.dot{width:.42rem;height:.42rem;margin-top:.35rem;border-radius:50%;background:transparent}.unread .dot{background:#176b52}.state{padding:1.25rem;color:#68746d}@media(max-width:40rem){.panel{position:fixed;top:4rem;right:.75rem;left:.75rem;width:auto}}`],
  changeDetection:ChangeDetectionStrategy.OnPush
})
export class NotificationMenuComponent{
  readonly service=inject(NotificationService);
  private readonly router=inject(Router);
  private readonly element=inject(ElementRef<HTMLElement>);
  readonly open=signal(false);
  toggle(){this.open.update(value=>!value);if(this.open())this.service.load();}
  openNotification(item:AppNotification){this.service.markRead(item);this.open.set(false);void this.router.navigateByUrl(item.link);}
  @HostListener('document:click',['$event']) closeOutside(event:MouseEvent){if(this.open()&&!this.element.nativeElement.contains(event.target as Node))this.open.set(false);}
}
