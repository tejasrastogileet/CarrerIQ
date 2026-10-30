import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

import { ResumeAnalysis } from '../models/resume-analysis.model';
import { ResumeAnalysisRequest } from '../models/resume-analysis-request.model';

@Injectable({
  providedIn: 'root'
})
export class ResumeService {

  private readonly http = inject(HttpClient);

  private readonly apiUrl =
    'http://localhost:5155/api/Resume';

  analyzeResume(
    request: ResumeAnalysisRequest
  ): Observable<ResumeAnalysis> {

    console.log(
      'Sending request to:',
      `${this.apiUrl}/analyze`
    );

    return this.http
      .post<ResumeAnalysis>(
        `${this.apiUrl}/analyze`,
        request
      )
      .pipe(

        tap(response => {

          console.log(
            '========== API RESPONSE =========='
          );

          console.log(response);

          console.log(
            '=================================='
          );

        })

      );
  }

  analyzeResumeFile(file: File): Observable<ResumeAnalysis> {
    const formData = new FormData();
    formData.append('file', file, file.name);

    return this.http.post<ResumeAnalysis>(
      `${this.apiUrl}/analyze-file`,
      formData
    );
  }
}