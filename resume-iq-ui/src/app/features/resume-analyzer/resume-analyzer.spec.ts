import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Observable, Subject, of, throwError } from 'rxjs';

import { ResumeAnalyzer } from './resume-analyzer';
import { ResumeService } from '../../core/services/resume';
import { ResumeAnalysis } from '../../core/models/resume-analysis.model';
import { ResumeAnalysisRequest } from '../../core/models/resume-analysis-request.model';

describe('ResumeAnalyzer', () => {
  let component: ResumeAnalyzer;
  let fixture: ComponentFixture<ResumeAnalyzer>;
  let analyzeResponse: Observable<ResumeAnalysis>;
  let receivedRequest: ResumeAnalysisRequest | undefined;
  let receivedFile: File | undefined;
  let fileAnalyzeCallCount: number;

  beforeEach(async () => {
    analyzeResponse = of({ skills: [], strengths: [], weaknesses: [], recommendations: [] });
    receivedRequest = undefined;
    receivedFile = undefined;
    fileAnalyzeCallCount = 0;

    await TestBed.configureTestingModule({
      imports: [ResumeAnalyzer],
      providers: [{
        provide: ResumeService,
        useValue: {
          analyzeResume: (request: ResumeAnalysisRequest) => {
            receivedRequest = request;
            return analyzeResponse;
          },
          analyzeResumeFile: (file: File) => {
            receivedFile = file;
            fileAnalyzeCallCount++;
            return analyzeResponse;
          }
        }
      }]
    }).compileComponents();

    fixture = TestBed.createComponent(ResumeAnalyzer);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('disables analysis while the resume text is empty', () => {
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('.analyze-button') as HTMLButtonElement;
    expect(button.disabled).toBe(true);
  });

  it('sends the existing resumeText request payload', () => {
    component.resumeText = 'Product designer with 8 years of experience';

    component.analyzeResume();

    expect(receivedRequest).toEqual({ resumeText: 'Product designer with 8 years of experience' });
    expect(component.isLoading()).toBe(false);
    expect(component.analysis()).toEqual({ skills: [], strengths: [], weaknesses: [], recommendations: [] });
  });

  it('clears loading and displays a useful error when analysis fails', () => {
    analyzeResponse = throwError(() => new HttpErrorResponse({
      status: 502,
      error: { message: 'Analysis service unavailable' }
    }));
    component.resumeText = 'Resume text';

    component.analyzeResume();

    expect(component.isLoading()).toBe(false);
    expect(component.errorMessage()).toBe('Analysis service unavailable');
  });

  it('accepts a content-validated CSV file', async () => {
    const file = new File(['Name,Skills\nTaylor,"SQL, Python"'], 'resume.csv', { type: 'text/csv' });

    await component.selectResumeFile(file);

    expect(component.selectedFile()).toBe(file);
    expect(component.errorMessage()).toBe('');
  });

  it('rejects unsupported, empty, oversized, and mismatched files', async () => {
    await component.selectResumeFile(new File(['content'], 'resume.txt'));
    expect(component.errorMessage()).toContain('Unsupported file format');

    await component.selectResumeFile(new File([], 'empty.csv'));
    expect(component.errorMessage()).toContain('empty');

    await component.selectResumeFile(new File([new Uint8Array(10 * 1024 * 1024 + 1)], 'large.csv'));
    expect(component.errorMessage()).toContain('10 MB');

    await component.selectResumeFile(new File(['not a PDF'], 'resume.pdf'));
    expect(component.errorMessage()).toContain('does not match');
    expect(component.selectedFile()).toBeNull();
  });

  it('rejects CSV files with unclosed quoted fields', async () => {
    await component.selectResumeFile(new File(['Name,Summary\nTaylor,"unfinished'], 'resume.csv'));

    expect(component.errorMessage()).toContain('unclosed quoted field');
    expect(component.selectedFile()).toBeNull();
  });

  it('uploads the selected file and prevents duplicate analysis requests', async () => {
    const pendingResponse = new Subject<ResumeAnalysis>();
    analyzeResponse = pendingResponse;
    const file = new File(['Name,Skills\nTaylor,SQL'], 'resume.csv');
    await component.selectResumeFile(file);

    component.analyzeResume();
    component.analyzeResume();

    expect(fileAnalyzeCallCount).toBe(1);
    expect(receivedFile).toBe(file);
    expect(component.isLoading()).toBe(true);

    pendingResponse.next({ skills: ['SQL'], strengths: [], weaknesses: [], recommendations: [] });
    pendingResponse.complete();
    expect(component.analysis()?.skills).toEqual(['SQL']);
    expect(component.isLoading()).toBe(false);
  });
});
