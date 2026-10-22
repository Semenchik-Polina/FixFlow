import LinearProgress from '@mui/material/LinearProgress';
import { useNavigation } from 'react-router';
import { strings } from '../../shared/strings';

/**
 * A thin bar along the bottom of the app bar while the next page loads. A component of its own, so a change of
 * navigation state re-renders only this bar, not the shell around the page.
 */
export function NavigationProgress() {
  const isLoading = useNavigation().state === 'loading';

  return isLoading ? (
    <LinearProgress
      color="secondary"
      aria-label={strings.common.loading}
      sx={{ position: 'absolute', bottom: 0, left: 0, right: 0 }}
    />
  ) : null;
}
