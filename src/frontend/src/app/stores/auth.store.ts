import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import http from '@/shared/api/http';
import router from '@/app/router';
import type { OfficerDto, LoginRequest, LoginResponse } from '@/shared/types/api.generated';

const TOKEN_KEY = 'prima_token';
const OFFICER_KEY = 'prima_officer';

function readStoredOfficer(): OfficerDto | null {
  const raw = localStorage.getItem(OFFICER_KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as OfficerDto;
  } catch {
    localStorage.removeItem(OFFICER_KEY);
    return null;
  }
}

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(localStorage.getItem(TOKEN_KEY));
  const officer = ref<OfficerDto | null>(readStoredOfficer());

  const isAuthenticated = computed(() => Boolean(token.value && officer.value));

  function restoreSession(): boolean {
    const storedToken = localStorage.getItem(TOKEN_KEY);
    const storedOfficer = readStoredOfficer();

    if (storedToken && storedOfficer) {
      token.value = storedToken;
      officer.value = storedOfficer;
      return true;
    }

    clearSession();
    return false;
  }

  async function login(username: string, password: string): Promise<LoginResponse> {
    const payload: LoginRequest = {
      username: username.trim(),
      password,
    };

    const response = await http.post<LoginResponse>('/auth/login', payload);
    const data = response.data;

    token.value = data.token;
    officer.value = data.officer;

    localStorage.setItem(TOKEN_KEY, data.token);
    localStorage.setItem(OFFICER_KEY, JSON.stringify(data.officer));

    return data;
  }

  function clearSession() {
    token.value = null;
    officer.value = null;
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(OFFICER_KEY);
  }

  function logout() {
    clearSession();
    router.push('/login');
  }

  return {
    token,
    officer,
    isAuthenticated,
    login,
    logout,
    restoreSession,
    clearSession,
  };
});
