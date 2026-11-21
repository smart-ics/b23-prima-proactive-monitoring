import type {
  DetectorListItemDto,
  DetectorSummaryDto,
  DetectorDetailDto,
  InspectionRunDetailDto,
  InspectionRunLogResponse,
  DetectorHealth,
  AdminStatus,
  TriggerSource,
  RegisterDetectorRequest,
  UpdateDetectorRequest,
  ToggleDetectorStatusRequest,
  InspectionRunResultDto,
} from '@/shared/types/api.generated';

export interface DetectorFilterState {
  scope: string;
  adminStatus: string;
  healthStatus: string;
  search: string;
}

export interface RunLogFilterState {
  startDateUtc?: string;
  endDateUtc?: string;
  outcome?: string;
  triggerSource?: string;
  limit?: number;
  offset?: number;
}

export type {
  DetectorListItemDto,
  DetectorSummaryDto,
  DetectorDetailDto,
  InspectionRunDetailDto,
  InspectionRunLogResponse,
  DetectorHealth,
  AdminStatus,
  TriggerSource,
  RegisterDetectorRequest,
  UpdateDetectorRequest,
  ToggleDetectorStatusRequest,
  InspectionRunResultDto,
};
