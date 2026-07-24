import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { NotificationService } from './notification.service';

describe('NotificationService', () => {
  it('loads notifications and updates unread state when one is read', () => {
    TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting()]});
    const service=TestBed.inject(NotificationService);
    const http=TestBed.inject(HttpTestingController);
    const notification={id:'notification-id',type:'OfferReceived' as const,title:'New offer',message:'Provider submitted an offer',
      link:'/rfqs/rfq-id/offers',createdAt:new Date().toISOString(),readAt:null,isRead:false};

    service.load();
    http.expectOne('/api/v1/notifications').flush([notification]);
    expect(service.unreadCount()).toBe(1);

    service.markRead(notification);
    const read=http.expectOne('/api/v1/notifications/notification-id/read');
    expect(read.request.method).toBe('POST');
    read.flush({...notification,isRead:true,readAt:new Date().toISOString()});
    expect(service.unreadCount()).toBe(0);
    expect(service.notifications()[0].isRead).toBe(true);
    http.verify();
  });
});
