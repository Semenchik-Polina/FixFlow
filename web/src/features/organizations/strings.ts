export const organizationStrings = {
  picker: {
    title: 'Your organizations',
    intro: 'Choose where to work. You can switch at any time from the top bar.',
    empty: "You're not a member of any organization yet. Create one, or ask a manager to invite you.",
    create: 'Create organization',
  },
  create: {
    title: 'Create an organization',
    intro: 'You become its admin and can invite your team afterwards.',
    submit: 'Create organization',
    cancel: 'Back to your organizations',
  },
  notFound: {
    title: 'Organization not found',
    detail: (slug: string) =>
      `There's no organization at /${slug} that you can open. Check the address, or choose one of your organizations.`,
    detailNoSlug: "There's no organization at this address that you can open. Choose one of your organizations.",
    yourOrganizations: 'Your organizations',
  },
} as const;
