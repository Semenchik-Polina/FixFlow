import LinearProgress from '@mui/material/LinearProgress';
import { strings } from '../strings';

/** Shown while a page's code or the app's first route loads. */
export function PageLoader() {
  return <LinearProgress aria-label={strings.common.loading} />;
}
