<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { useRoute, RouterLink, RouterView } from 'vue-router';
import { useAuthStore } from '@/app/stores/auth.store';
import { activeIssuesCount } from '@/shared/composables/useAppMetrics';

const route = useRoute();
const authStore = useAuthStore();

// Live WIB Clock (UTC+7 / Asia/Jakarta)
const currentTimeWib = ref<string>('');
let clockTimer: ReturnType<typeof setInterval> | null = null;

function formatWibTime(): string {
  const now = new Date();
  const dateStr = new Intl.DateTimeFormat('en-GB', {
    timeZone: 'Asia/Jakarta',
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  }).format(now);

  const timeStr = new Intl.DateTimeFormat('en-GB', {
    timeZone: 'Asia/Jakarta',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false,
  }).format(now);

  return `${dateStr}, ${timeStr} WIB`;
}

onMounted(() => {
  currentTimeWib.value = formatWibTime();
  clockTimer = setInterval(() => {
    currentTimeWib.value = formatWibTime();
  }, 1000);
});

onUnmounted(() => {
  if (clockTimer) {
    clearInterval(clockTimer);
    clockTimer = null;
  }
});

// Officer Attribution
const officerName = computed(() => {
  return authStore.officer?.fullName || authStore.officer?.username || 'Officer';
});

const officerRoleLabel = computed(() => {
  const role = authStore.officer?.role;
  switch (role) {
    case 'EdpOfficer':
      return 'EDP Officer';
    case 'SystemAnalyst':
      return 'System Analyst';
    case 'Lead':
      return 'EDP Lead';
    default:
      return role || 'Officer';
  }
});

const officerInitials = computed(() => {
  const name = officerName.value.trim();
  const parts = name.split(/\s+/);
  if (parts.length >= 2) {
    return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
  }
  return name.slice(0, 2).toUpperCase();
});

const environmentMode = computed(() => {
  return (import.meta.env.MODE || 'development').toUpperCase();
});

function handleLogout() {
  authStore.logout();
}
</script>

<template>
  <div class="min-h-screen bg-slate-50 flex flex-col antialiased text-slate-900">
    <!-- Top Global Header -->
    <header class="h-14 bg-white border-b border-slate-200 px-4 sm:px-6 flex items-center justify-between sticky top-0 z-20">
      <!-- Left: Brand Title -->
      <div class="flex items-center gap-3">
        <div class="flex items-center justify-center w-8 h-8 rounded-md bg-slate-900 text-white font-bold text-sm">
          P
        </div>
        <div>
          <div class="flex items-center gap-2">
            <span class="font-bold text-slate-900 text-base leading-none">PrIMA</span>
            <span class="text-[10px] font-mono px-1.5 py-0.5 rounded bg-slate-100 text-slate-600 border border-slate-200 font-semibold">Stage 1</span>
          </div>
          <p class="text-[11px] text-slate-500 leading-none mt-0.5 hidden sm:block">Proactive Inspection Monitoring App</p>
        </div>
      </div>

      <!-- Center: Live WIB Clock -->
      <div class="hidden md:flex items-center gap-2 text-xs font-mono font-medium text-slate-700 bg-slate-100/80 px-3 py-1.5 rounded-md border border-slate-200">
        <i class="pi pi-clock text-slate-500 text-[11px]"></i>
        <span>{{ currentTimeWib }}</span>
      </div>

      <!-- Right: Officer Profile & Logout -->
      <div class="flex items-center gap-3">
        <div class="flex items-center gap-2 text-right">
          <div class="hidden sm:block">
            <p class="text-xs font-semibold text-slate-900 leading-tight">{{ officerName }}</p>
            <span class="inline-block text-[10px] font-medium px-1.5 py-0.2 rounded bg-slate-100 text-slate-600 border border-slate-200">
              {{ officerRoleLabel }}
            </span>
          </div>
          <div class="w-8 h-8 rounded-full bg-slate-800 text-white text-xs font-semibold flex items-center justify-center">
            {{ officerInitials }}
          </div>
        </div>

        <div class="h-5 w-px bg-slate-200"></div>

        <button
          @click="handleLogout"
          class="p-1.5 text-slate-500 hover:text-rose-600 hover:bg-rose-50 rounded-md transition-colors flex items-center gap-1.5 text-xs font-medium"
          title="Sign Out"
        >
          <i class="pi pi-sign-out"></i>
          <span class="hidden sm:inline">Logout</span>
        </button>
      </div>
    </header>

    <!-- App Body (Sidebar + Content Canvas) -->
    <div class="flex flex-1 min-h-0">
      <!-- Fixed Left Sidebar (240px) -->
      <aside class="w-60 bg-white border-r border-slate-200 flex flex-col shrink-0">
        <!-- Sidebar Navigation -->
        <nav class="p-3 space-y-1.5 flex-1">
          <div class="px-3 py-1.5 text-[10px] font-semibold text-slate-400 uppercase tracking-wider">
            Operational Menu
          </div>

          <!-- 1. Dashboard (Active Route) -->
          <RouterLink
            to="/"
            class="flex items-center justify-between px-3 py-2 rounded-md text-sm transition-colors"
            :class="[
              route.path === '/'
                ? 'bg-slate-900 text-white font-medium shadow-xs'
                : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900'
            ]"
          >
            <div class="flex items-center gap-2.5">
              <i class="pi pi-th-large text-sm"></i>
              <span>Dashboard</span>
            </div>
            <!-- Open + Reopened count badge pill -->
            <span
              class="text-[11px] font-mono px-2 py-0.5 rounded-full"
              :class="[
                activeIssuesCount > 0
                  ? (route.path === '/' ? 'bg-rose-500 text-white font-bold' : 'bg-rose-100 text-rose-700 font-bold border border-rose-200')
                  : (route.path === '/' ? 'bg-slate-800 text-slate-300' : 'bg-slate-100 text-slate-600')
              ]"
            >
              {{ activeIssuesCount }}
            </span>
          </RouterLink>

          <!-- 2. Detectors (Disabled in Stage 1 - Coming in Stage 3) -->
          <div
            v-tooltip.right="'Coming in Stage 3'"
            aria-disabled="true"
            class="flex items-center justify-between px-3 py-2 rounded-md text-sm text-slate-400 opacity-60 cursor-not-allowed select-none hover:bg-transparent"
          >
            <div class="flex items-center gap-2.5">
              <i class="pi pi-shield text-sm"></i>
              <span>Detectors</span>
            </div>
            <span class="text-[10px] font-mono uppercase px-1.5 py-0.5 rounded bg-slate-100 text-slate-400 border border-slate-200">
              Stage 3
            </span>
          </div>

          <!-- 3. History & Reports (Disabled in Stage 1 - Coming in Stage 4) -->
          <div
            v-tooltip.right="'Coming in Stage 4'"
            aria-disabled="true"
            class="flex items-center justify-between px-3 py-2 rounded-md text-sm text-slate-400 opacity-60 cursor-not-allowed select-none hover:bg-transparent"
          >
            <div class="flex items-center gap-2.5">
              <i class="pi pi-history text-sm"></i>
              <span>History & Reports</span>
            </div>
            <span class="text-[10px] font-mono uppercase px-1.5 py-0.5 rounded bg-slate-100 text-slate-400 border border-slate-200">
              Stage 4
            </span>
          </div>
        </nav>

        <!-- Sidebar Footer: Environment & Connection Status -->
        <div class="p-3 border-t border-slate-200 bg-slate-50/70 space-y-2 text-xs">
          <div class="flex items-center justify-between">
            <span class="text-slate-500 text-[11px]">Environment</span>
            <span class="font-mono text-[10px] font-bold px-1.5 py-0.5 rounded bg-amber-100 text-amber-800 border border-amber-300">
              {{ environmentMode }}
            </span>
          </div>
          <div class="flex items-center justify-between">
            <span class="text-slate-500 text-[11px]">Connection</span>
            <div class="flex items-center gap-1.5 text-[11px] font-medium text-emerald-700">
              <span class="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span>
              <span>Live · 10s</span>
            </div>
          </div>
        </div>
      </aside>

      <!-- Main Operational Canvas -->
      <main class="flex-1 min-w-0 bg-slate-50 overflow-y-auto">
        <div class="max-w-[1440px] mx-auto px-6 py-5">
          <RouterView />
        </div>
      </main>
    </div>

    <!-- Layout Bottom Footer -->
    <footer class="h-8 bg-white border-t border-slate-200 px-4 sm:px-6 flex items-center justify-between text-[11px] text-slate-500 shrink-0">
      <div class="flex items-center gap-2">
        <span class="font-semibold text-slate-700">PrIMA SIMRS Operational Workbench</span>
        <span>&middot;</span>
        <span>Hospital Information Systems Proactive Monitoring</span>
      </div>
      <div class="flex items-center gap-3">
        <span class="flex items-center gap-1.5 text-emerald-700 font-medium">
          <span class="w-1.5 h-1.5 rounded-full bg-emerald-500"></span>
          Operational
        </span>
        <span>&middot;</span>
        <span class="font-mono text-[10px] text-slate-400">v1.0-stage1</span>
      </div>
    </footer>
  </div>
</template>
