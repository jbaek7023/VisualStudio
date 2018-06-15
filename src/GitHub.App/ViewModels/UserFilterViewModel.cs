﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using GitHub.Extensions;
using GitHub.Models;
using ReactiveUI;

namespace GitHub.ViewModels
{
    public class UserFilterViewModel : ViewModelBase, IUserFilterViewModel
    {
        readonly LoadPageDelegate load;
        ReactiveList<IActorViewModel> users;
        ListCollectionView usersView;
        string filter;
        IActorViewModel selected;
        IActorViewModel ersatzUser;

        public delegate Task<Page<ActorModel>> LoadPageDelegate(string after);

        public UserFilterViewModel(LoadPageDelegate load)
        {
            this.load = load;
            this.WhenAnyValue(x => x.Filter).Subscribe(FilterChanged);
            ClearSelection = ReactiveCommand.Create(
                this.WhenAnyValue(x => x.Selected).Select(x => x != null))
                .OnExecuteCompleted(_ => Selected = null);
        }

        public IReadOnlyList<IActorViewModel> Users
        {
            get
            {
                if (users == null)
                {
                    users = new ReactiveList<IActorViewModel>();
                    Load().Forget();
                }

                return users;
            }
        }

        public ICollectionView UsersView
        {
            get
            {
                if (usersView == null)
                {
                    usersView = new ListCollectionView((IList)Users);
                    usersView.CustomSort = new UserComparer(this);
                    usersView.Filter = FilterUsers;
                }

                return usersView;
            }
        }

        public string Filter
        {
            get { return filter; }
            set { this.RaiseAndSetIfChanged(ref filter, value); }
        }

        public IActorViewModel Selected
        {
            get { return selected; }
            set { this.RaiseAndSetIfChanged(ref selected, value); }
        }

        public ReactiveCommand<object> ClearSelection { get; }

        void FilterChanged(string filter)
        {
            if (users == null) return;

            if (ersatzUser != null)
            {
                users.Remove(ersatzUser);
                ersatzUser = null;
            }

            if (!string.IsNullOrWhiteSpace(filter))
            {
                var existing = users.FirstOrDefault(x => x.Login.Equals(filter, StringComparison.CurrentCultureIgnoreCase));

                if (existing == null)
                {
                    ersatzUser = new ActorViewModel(new ActorModel { Login = filter });
                    users.Add(ersatzUser);
                }
            }

            usersView.Refresh();
        }

        bool FilterUsers(object obj)
        {
            if (Filter != null)
            {
                var user = obj as IActorViewModel;
                return user?.Login.IndexOf(Filter, StringComparison.CurrentCultureIgnoreCase) != -1;
            }

            return true;
        }

        async Task Load()
        {
            string after = null;

            while (true)
            {
                var page = await load(after);
                users.AddRange(page.Items.Select(x => new ActorViewModel(x)));
                after = page.EndCursor;
                if (!page.HasNextPage) break;
            }
        }

        class UserComparer : IComparer
        {
            readonly UserFilterViewModel owner;

            public UserComparer(UserFilterViewModel owner)
            {
                this.owner = owner;
            }

            public int Compare(object x, object y)
            {
                if (x == owner.ersatzUser) return -1;
                if (y == owner.ersatzUser) return 1;
                return ((IActorViewModel)x).Login.CompareTo(((IActorViewModel)y).Login);
            }
        }
    }
}