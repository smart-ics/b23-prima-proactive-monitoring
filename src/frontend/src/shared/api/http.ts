import axios, { type AxiosInstance, type InternalAxiosRequestConfig, AxiosError } from 'axios';
import { useAuthStore } from '@/app/stores/auth.store';
import router from '@/app/router';
import { ApiError } from '@/shared/api/errors';
import type { ProblemDetails } from '@/shared/types/api.generated';
import { getSafeRedirect } from '@/shared/utils/security';

export const http: AxiosInstance = axios.create({
  baseURL: '/api',
  headers: {
    'Content-Type': 'application/json',
  },
});

http.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    try {
      const authStore = useAuthStore();
      if (authStore.token) {
        config.headers.set('Authorization', `Bearer ${authStore.token}`);
      }
    } catch {
      // In case store is accessed before Pinia initialization in isolated environments
      const rawToken = localStorage.getItem('prima_token');
      if (rawToken) {
        config.headers.set('Authorization', `Bearer ${rawToken}`);
      }
    }
    return config;
  },
  (error) => Promise.reject(error)
);

http.interceptors.response.use(
  (response) => response,
  (error: AxiosError<ProblemDetails>) => {
    const status = error.response?.status;
    const data = error.response?.data;
    const problemDetails: ProblemDetails | undefined =
      data && typeof data === 'object' ? data : undefined;

    if (status === 401) {
      try {
        const authStore = useAuthStore();
        authStore.clearSession();
      } catch {
        localStorage.removeItem('prima_token');
        localStorage.removeItem('prima_officer');
      }

      const currentRoute = router.currentRoute.value;
      if (currentRoute.path !== '/login') {
        const fullPath = currentRoute.fullPath;
        const safePath = getSafeRedirect(fullPath, '/');
        router.push({
          path: '/login',
          query: safePath !== '/' ? { redirect: safePath } : undefined,
        });
      }
    }

    const message =
      problemDetails?.detail ||
      problemDetails?.title ||
      error.message ||
      'An unexpected network error occurred';

    const apiError = new ApiError(message, problemDetails, status);
    return Promise.reject(apiError);
  }
);

export { ApiError } from '@/shared/api/errors';
export default http;
