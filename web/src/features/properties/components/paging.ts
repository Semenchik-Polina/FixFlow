import type { GridPaginationModel } from '@mui/x-data-grid';

/** The API's default page size first; 100 is its largest (`PageSizes.Max`). */
export const PAGE_SIZE_OPTIONS = [25, 50, 100];

export const FIRST_PAGE: GridPaginationModel = { page: 0, pageSize: 25 };
