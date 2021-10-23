import { BrowserModule } from '@angular/platform-browser';
import { NgModule } from '@angular/core';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { RequestRowComponent } from './request-row/request-row.component';
import { RequestReviewPanelComponent } from './request-review-panel/request-review-panel.component';
import { OverlayModule } from '@angular/cdk/overlay';
import { ResultsTableComponent } from './results-table/results-table.component';
import { HttpClientModule, HTTP_INTERCEPTORS } from '@angular/common/http';
import { DashboardComponent } from './dashboard/dashboard.component';
import { HttpErrorInterceptor } from './http-error.interceptor';
import { SpinnerComponent } from './spinner/spinner.component';
import { SpinnerOverlayComponent } from './spinner-overlay/spinner-overlay.component';
import { CommonModule } from '@angular/common';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';

@NgModule({
  declarations: [
    AppComponent,
    RequestRowComponent,
    RequestReviewPanelComponent,
    ResultsTableComponent,
    DashboardComponent,
    SpinnerComponent,
    SpinnerOverlayComponent
  ],
  imports: [
    BrowserModule,
    AppRoutingModule,
    OverlayModule,
    HttpClientModule,
    CommonModule,
    FontAwesomeModule
  ],
  providers: [
    {
      provide: HTTP_INTERCEPTORS,

      useClass: HttpErrorInterceptor,
 
      multi: true
    }
  ],
  bootstrap: [AppComponent]
})
export class AppModule {

  constructor(){
  }
 }
