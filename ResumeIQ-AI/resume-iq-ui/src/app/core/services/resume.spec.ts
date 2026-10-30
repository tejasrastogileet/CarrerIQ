import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ResumeService } from './resume';

describe('Resume', () => {
  let service: ResumeService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(ResumeService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('posts the unchanged resume payload to the analysis endpoint', () => {
    const requestBody = { resumeText: 'Product designer with 8 years of experience' };
    const response = { skills: [], strengths: [], weaknesses: [], recommendations: [] };

    service.analyzeResume(requestBody).subscribe();

    const request = httpTestingController.expectOne('http://localhost:5155/api/Resume/analyze');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(requestBody);
    request.flush(response);
    httpTestingController.verify();
  });

  it('uploads files as multipart form data without setting a content type', () => {
    const file = new File(['Name,Skills\nTaylor,SQL'], 'resume.csv', { type: 'text/csv' });
    const response = { skills: [], strengths: [], weaknesses: [], recommendations: [] };

    service.analyzeResumeFile(file).subscribe();

    const request = httpTestingController.expectOne('http://localhost:5155/api/Resume/analyze-file');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBeInstanceOf(FormData);
    expect((request.request.body as FormData).get('file')).toMatchObject({
      name: 'resume.csv'
    });
    expect(request.request.headers.has('Content-Type')).toBe(false);
    request.flush(response);
    httpTestingController.verify();
  });
});
