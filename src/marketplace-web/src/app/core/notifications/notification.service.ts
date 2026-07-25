import { HttpClient } from '@angular/common/http';
import { DestroyRef, effect, inject, Injectable, signal } from '@angular/core';
import { AuthService } from '../auth/auth.service';

export type NotificationType = 'OfferReceived' | 'OfferRevised';
export interface AppNotification {
  id:string; type:NotificationType; title:string; message:string; link:string;
  createdAt:string; readAt:string|null; isRead:boolean;
}

@Injectable({providedIn:'root'})
export class NotificationService {
  private readonly http=inject(HttpClient);
  private readonly auth=inject(AuthService);
  private readonly destroyRef=inject(DestroyRef);
  private timer:ReturnType<typeof setInterval>|null=null;
  readonly unreadCount=signal(0);
  readonly notifications=signal<AppNotification[]>([]);
  readonly loading=signal(false);

  constructor(){
    effect(()=>{
      if(this.auth.isAuthenticated()){this.refreshCount();this.startPolling();}
      else{this.stopPolling();this.unreadCount.set(0);this.notifications.set([]);}
    });
    this.destroyRef.onDestroy(()=>this.stopPolling());
  }
  load(){
    this.loading.set(true);
    this.http.get<AppNotification[]>('/api/v1/notifications').subscribe({
      next:values=>{this.notifications.set(values);this.unreadCount.set(values.filter(item=>!item.isRead).length);this.loading.set(false);},
      error:()=>this.loading.set(false)
    });
  }
  refreshCount(){this.http.get<{count:number}>('/api/v1/notifications/unread-count').subscribe({next:value=>this.unreadCount.set(value.count),error:()=>undefined});}
  markRead(item:AppNotification){
    if(item.isRead)return;
    this.http.post<AppNotification>(`/api/v1/notifications/${item.id}/read`,{}).subscribe({
      next:value=>{this.notifications.update(items=>items.map(current=>current.id===value.id?value:current));this.unreadCount.update(count=>Math.max(0,count-1));}
    });
  }
  markAllRead(){
    this.http.post<void>('/api/v1/notifications/read-all',{}).subscribe({next:()=>{this.notifications.update(items=>items.map(item=>({...item,isRead:true,readAt:new Date().toISOString()})));this.unreadCount.set(0);}});
  }
  private startPolling(){if(this.timer===null)this.timer=setInterval(()=>this.refreshCount(),30_000);}
  private stopPolling(){if(this.timer!==null)clearInterval(this.timer);this.timer=null;}
}
