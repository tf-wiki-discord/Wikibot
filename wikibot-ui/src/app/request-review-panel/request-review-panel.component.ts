import { Component, OnInit, Inject, InjectionToken, ViewEncapsulation, EventEmitter, Output } from '@angular/core';
import { Request } from '../request';
import { RequestService } from '../request.service';
import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { DiffService } from '../diff.service';
import { CommonModule } from '@angular/common';
import { faWindowClose } from '@fortawesome/free-solid-svg-icons';
import { faAngleLeft } from '@fortawesome/free-solid-svg-icons';
import { faAngleRight } from '@fortawesome/free-solid-svg-icons';

export const CONTAINER_DATA = new InjectionToken<{}>('CONTAINER_DATA');

@Component({
  selector: 'app-request-review-panel',
  templateUrl: './request-review-panel.component.html',
  styleUrls: ['./request-review-panel.component.css'],
  encapsulation: ViewEncapsulation.None
})

export class RequestReviewPanelComponent implements OnInit {
  @Output() closePanel = new EventEmitter<void>();
  loading: boolean;
  error: boolean;
  success: boolean;
  success_message: string;
  error_message: string;
  original_request_status: string;
  currPage: number = 1;
  lastPage: number = 1;
  showRaw: boolean = true;
  showDetails: boolean = true;
  showDiff: boolean = false;
  actionTaken: boolean;
  currDiffHTML$: Observable<string>;
  faWindowClose = faWindowClose;
  faAngleLeft = faAngleLeft;
  faAngleRight = faAngleRight;
  public request:Request;
  private _requestService;
  private _diffService:DiffService;
  
  constructor( @Inject(CONTAINER_DATA) public componentData:any, private requestService: RequestService, private diffService: DiffService) { 
    this.request = componentData;
    //this.request.diffPaths = ["Diff-Alana-0-0.txt", "Diff-Alien-0-0.txt", "Diff-Comics-0-0.txt"];
    this.lastPage = this.request.diffs.length;
    this._requestService = requestService;
    this._diffService = diffService;
    this.original_request_status = this.request.statusName;
  }


  ngOnInit() {
    if(this.request.diffs.length > 0){
    this.currDiffHTML$ = this._diffService.getDiff(this.request.diffs[0], this.request.id.toString());
    }
  }

  nextPage(){
    if(this.currPage < this.lastPage){
      this.currPage++;
      this.currDiffHTML$ = this._diffService.getDiff(this.request.diffs[this.currPage-1], this.request.id.toString());
    }
  }

  prevPage(){
    if(this.currPage > 1){
      this.currPage--;
      this.currDiffHTML$ = this._diffService.getDiff(this.request.diffs[this.currPage-1], this.request.id.toString());
    }
  }

  preApprove(){
    if(!this.success && !this.error)
    {
      this.loading = true;
      var result = this._requestService.preApproveRequest(this.request.id).subscribe(
        res => {
          this.error = false;
          this.loading = false;
          this.success = true;
          this.success_message = "Request has been preapproved.";
          this.request.statusName = 'PreApproved';
        },
        err=> {
          console.error(err);
          this.loading = false;
          this.error = true;
          this.success = false;
          this.error_message = err;
        }
      );
    }
  }

  Approve(){
    if(!this.success && !this.error)
    {
      this.loading = true;
      var result = this._requestService.ApproveRequest(this.request.id).subscribe(
        res => {
          this.error = false;
          this.loading = false;
          this.success = true;
          this.success_message = "Request has been approved.";
          this.request.statusName = "Approved";
        },
        err=> {
          console.error(err);
          this.loading = false;
          this.error = true;
          this.success = false;
          this.error_message = err;
        }
      );
    }
  }

  Reject(){
    if(!this.success && !this.error)
    {
      this.loading = true;
      var result = this._requestService.RejectRequest(this.request.id).subscribe(
        res => {
          this.error = false;
          this.loading = false;
          this.success = true;
          this.success_message = "Request has been rejected.";
          this.request.statusName = 'Rejected';
        },
        err=> {
          console.error(err);
          this.loading = false;
          this.error = true;
          this.success = false;
          this.error_message = err;
        }
      );
    }
  }

  close() {
    this.closePanel.emit();
  }
}
