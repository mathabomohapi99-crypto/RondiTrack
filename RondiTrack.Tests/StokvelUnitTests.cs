using RondiTrack.Domain;
using Xunit;

namespace RondiTrack.Tests;

public class StokvelMembershipTests
{
    [Fact]
    public void AddMember_Throws_When_Stokvel_Is_Full()
    {
        var stokvel = new Stokvel("Test", 100m, ContributionFrequency.Monthly, 2);
        var user1 = new User("A", "a@example.com");
        var user2 = new User("B", "b@example.com");
        var user3 = new User("C", "c@example.com");
        stokvel.AddMember(user1);
        stokvel.AddMember(user2);

        Assert.Throws<DomainConflictException>(() => stokvel.AddMember(user3));
    }

    [Fact]
    public void AddMember_Throws_When_User_Already_A_Member()
    {
        var stokvel = new Stokvel("Test", 100m, ContributionFrequency.Monthly, 5);
        var user = new User("A", "a@example.com");
        stokvel.AddMember(user);

        Assert.Throws<DomainConflictException>(() => stokvel.AddMember(user));
    }

    [Fact]
    public void AddMember_Succeeds_When_Space_Available_And_Not_Already_Member()
    {
        var stokvel = new Stokvel("Test", 100m, ContributionFrequency.Monthly, 5);
        var user = new User("A", "a@example.com");

        stokvel.AddMember(user);

        Assert.True(stokvel.HasMember(user.Id));
    }
}